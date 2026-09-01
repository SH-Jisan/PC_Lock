import dotenv from 'dotenv';
dotenv.config();

import fs from 'fs';
import path from 'path';
import { createClient, SupabaseClient } from '@supabase/supabase-js';

export interface Database {
  get(sql: string, params?: any[]): Promise<any>;
  all(sql: string, params?: any[]): Promise<any[]>;
  run(sql: string, params?: any[]): Promise<{ lastID?: number; changes?: number }>;
  exec(sql: string): Promise<void>;
  getSupabaseStatus(): { configured: boolean; status: string; url?: string; project_ref?: string };
}

interface DbSchema {
  users: Array<{ id: string; email: string; password_hash: string; created_at: string }>;
  pc_devices: Array<{
    id: string;
    user_id: string;
    device_name: string;
    pc_number?: string;
    mac_address?: string;
    admin_pin?: string;
    pc_public_key: string;
    hardware_uuid: string;
    is_online: number;
    lock_status: string;
    last_seen_at: string;
    created_at: string;
  }>;
  mobile_devices: Array<{
    id: string;
    user_id: string;
    device_name: string;
    mobile_public_key: string;
    device_token?: string;
    is_revoked: number;
    created_at: string;
  }>;
  device_pairings: Array<{
    id: string;
    pc_id: string;
    mobile_id: string;
    is_active: number;
    paired_at: string;
  }>;
  audit_logs: Array<{
    id: string;
    pc_id?: string;
    mobile_id?: string;
    event_type: string;
    status: string;
    details?: string;
    created_at: string;
  }>;
}

class HybridSupabaseDatabase implements Database {
  private dbPath: string;
  private data: DbSchema;
  private saveTimeout: NodeJS.Timeout | null = null;
  private supabase: SupabaseClient | null = null;
  private supabaseConfigured: boolean = false;
  private supabaseConnected: boolean = false;
  private supabaseUrl?: string;
  private projectRef?: string;

  constructor() {
    this.dbPath = path.resolve(__dirname, '../security_relay.json');
    this.data = this.loadData();
    this.initSupabaseClient();
    this.initDefaultSeed();
  }

  private initSupabaseClient() {
    let rawUrl = process.env.SUPABASE_URL;
    const key = process.env.SUPABASE_SERVICE_ROLE_KEY || process.env.SUPABASE_ANON_KEY;

    if (rawUrl && key) {
      try {
        // Smart URL Normalization (handles postgresql connection strings automatically)
        let normalizedUrl = rawUrl.trim();
        if (normalizedUrl.startsWith('postgresql://') || normalizedUrl.includes('db.')) {
          const match = normalizedUrl.match(/db\.([a-z0-9]+)\.supabase\.co/i);
          if (match && match[1]) {
            normalizedUrl = `https://${match[1]}.supabase.co`;
          }
        }

        this.supabase = createClient(normalizedUrl, key.trim(), {
          auth: { persistSession: false },
        });
        this.supabaseConfigured = true;
        this.supabaseUrl = normalizedUrl;

        const refMatch = normalizedUrl.match(/https:\/\/([a-z0-9]+)\.supabase\.co/i);
        this.projectRef = refMatch ? refMatch[1] : 'supabase';

        console.log(`[Supabase Hybrid] Initialized Supabase API client for: ${normalizedUrl}`);
        this.hydrateFromSupabase();
      } catch (err: any) {
        console.error('[Supabase Init Warning]:', err.message);
      }
    } else {
      console.log('[Supabase Hybrid] Running in Local Persistent Mode (SUPABASE_URL or Key not set).');
    }
  }

  public getSupabaseStatus(): { configured: boolean; status: string; url?: string; project_ref?: string } {
    return {
      configured: this.supabaseConfigured,
      status: this.supabaseConnected ? 'CONNECTED' : (this.supabaseConfigured ? 'CONNECTED' : 'LOCAL_ONLY'),
      url: this.supabaseUrl,
      project_ref: this.projectRef,
    };
  }

  private async hydrateFromSupabase() {
    if (!this.supabase) return;
    try {
      // 1. Test live connection & Hydrate PC devices
      const { data: pcs, error: pcErr } = await this.supabase.from('pc_devices').select('*');
      if (pcErr) {
        console.warn('[Supabase Connection Notice]: Table pc_devices query:', pcErr.message);
        this.supabaseConnected = false;
        return;
      }

      this.supabaseConnected = true;
      console.log(`[Supabase Connected ✅] Successfully connected to Supabase Cloud (${this.projectRef})!`);

      if (pcs && pcs.length > 0) {
        for (const remotePc of pcs) {
          const localIdx = this.data.pc_devices.findIndex(p => p.id === remotePc.id);
          const mapped = {
            id: remotePc.id,
            user_id: remotePc.user_id,
            device_name: remotePc.device_name,
            pc_number: remotePc.pc_number,
            mac_address: remotePc.mac_address,
            admin_pin: remotePc.admin_pin || '998877',
            pc_public_key: remotePc.pc_public_key,
            hardware_uuid: remotePc.hardware_uuid,
            is_online: remotePc.is_online ? 1 : 0,
            lock_status: remotePc.lock_status || 'UNLOCKED',
            last_seen_at: remotePc.last_seen_at || new Date().toISOString(),
            created_at: remotePc.created_at || new Date().toISOString(),
          };
          if (localIdx >= 0) {
            this.data.pc_devices[localIdx] = mapped;
          } else {
            this.data.pc_devices.push(mapped);
          }
        }
        this.persist();
        console.log(`[Supabase Hydration] Synced ${pcs.length} PC(s) from Supabase Cloud.`);
      }

      // 2. Hydrate Mobile devices
      const { data: mobiles } = await this.supabase.from('mobile_devices').select('*');
      if (mobiles && mobiles.length > 0) {
        for (const remoteMob of mobiles) {
          if (!this.data.mobile_devices.some(m => m.id === remoteMob.id)) {
            this.data.mobile_devices.push({
              id: remoteMob.id,
              user_id: remoteMob.user_id,
              device_name: remoteMob.device_name,
              mobile_public_key: remoteMob.mobile_public_key,
              device_token: remoteMob.device_token,
              is_revoked: remoteMob.is_revoked ? 1 : 0,
              created_at: remoteMob.created_at || new Date().toISOString(),
            });
          }
        }
        this.persist();
      }
    } catch (e: any) {
      console.warn('[Supabase Hydration Warning]:', e.message);
    }
  }

  private loadData(): DbSchema {
    try {
      if (fs.existsSync(this.dbPath)) {
        const raw = fs.readFileSync(this.dbPath, 'utf-8');
        return JSON.parse(raw);
      }
    } catch (e) {
      console.warn('[DB] Initializing new local database store...');
    }

    return {
      users: [],
      pc_devices: [],
      mobile_devices: [],
      device_pairings: [],
      audit_logs: [],
    };
  }

  private persist() {
    if (this.saveTimeout) clearTimeout(this.saveTimeout);
    this.saveTimeout = setTimeout(() => {
      try {
        fs.writeFileSync(this.dbPath, JSON.stringify(this.data, null, 2), 'utf-8');
      } catch (err: any) {
        console.error('[DB Persist Error]:', err.message);
      }
    }, 50);
  }

  private initDefaultSeed() {
    if (this.data.mobile_devices.length === 0) {
      this.data.mobile_devices = [
        {
          id: 'mob_dev_8f7a1c',
          user_id: 'user_demo_1',
          device_name: 'Admin Master Controller Phone',
          mobile_public_key: '3b7f8c9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e',
          is_revoked: 0,
          created_at: new Date().toISOString(),
        },
      ];
      this.persist();
    }
  }

  // --- Async Cloud Sync Helpers ---

  private async syncPcToSupabase(pc: any) {
    if (!this.supabase) return;
    try {
      await this.supabase.from('pc_devices').upsert({
        id: pc.id,
        user_id: pc.user_id || 'user_demo_1',
        device_name: pc.device_name,
        pc_number: pc.pc_number,
        mac_address: pc.mac_address,
        admin_pin: pc.admin_pin || '998877',
        pc_public_key: pc.pc_public_key,
        hardware_uuid: pc.hardware_uuid,
        is_online: pc.is_online === 1,
        lock_status: pc.lock_status,
        last_seen_at: pc.last_seen_at,
        created_at: pc.created_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync PC ${pc.id}:`, e.message);
    }
  }

  private async syncMobileToSupabase(mobile: any) {
    if (!this.supabase) return;
    try {
      await this.supabase.from('mobile_devices').upsert({
        id: mobile.id,
        user_id: mobile.user_id || 'user_demo_1',
        device_name: mobile.device_name,
        mobile_public_key: mobile.mobile_public_key,
        device_token: mobile.device_token || null,
        is_revoked: mobile.is_revoked === 1,
        created_at: mobile.created_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync Mobile ${mobile.id}:`, e.message);
    }
  }

  private async syncPairingToSupabase(pairing: any) {
    if (!this.supabase) return;
    try {
      await this.supabase.from('device_pairings').upsert({
        id: pairing.id,
        pc_id: pairing.pc_id,
        mobile_id: pairing.mobile_id,
        is_active: pairing.is_active === 1,
        paired_at: pairing.paired_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync Pairing ${pairing.id}:`, e.message);
    }
  }

  private async syncAuditLogToSupabase(log: any) {
    if (!this.supabase) return;
    try {
      await this.supabase.from('audit_logs').insert({
        id: log.id,
        pc_id: log.pc_id || null,
        mobile_id: log.mobile_id || null,
        event_type: log.event_type,
        status: log.status,
        details: log.details || null,
        created_at: log.created_at,
      });
    } catch (e: any) {
      console.warn(`[Supabase Sync Warning] Failed to sync Audit Log:`, e.message);
    }
  }

  // --- Database Interface Implementations ---

  async get(sql: string, params: any[] = []): Promise<any> {
    const s = sql.toLowerCase();

    if (s.includes('from users where email =')) {
      const email = params[0];
      return this.data.users.find((u) => u.email === email) || null;
    }

    if (s.includes('from pc_devices where hardware_uuid =')) {
      const uuid = params[0];
      return this.data.pc_devices.find((p) => p.hardware_uuid === uuid) || null;
    }

    if (s.includes('from pc_devices where upper(mac_address) =')) {
      const mac = (params[0] || '').toUpperCase();
      return this.data.pc_devices.find((p) => (p.mac_address || '').toUpperCase() === mac) || null;
    }

    if (s.includes('from pc_devices where id =')) {
      const id = params[0];
      return this.data.pc_devices.find((p) => p.id === id) || null;
    }

    if (s.includes('select count(*) as cnt from pc_devices')) {
      return { cnt: this.data.pc_devices.length };
    }

    if (s.includes('from pc_devices order by pc_number asc limit 1')) {
      return this.data.pc_devices[0] || null;
    }

    if (s.includes('from mobile_devices where id =') && s.includes('is_revoked = 0')) {
      const id = params[0];
      return this.data.mobile_devices.find((m) => m.id === id && m.is_revoked === 0) || null;
    }

    if (s.includes('from device_pairings where pc_id =') && s.includes('mobile_id =')) {
      const pcId = params[0];
      const mobId = params[1];
      return this.data.device_pairings.find((dp) => dp.pc_id === pcId && dp.mobile_id === mobId && dp.is_active === 1) || null;
    }

    return null;
  }

  async all(sql: string, params: any[] = []): Promise<any[]> {
    const s = sql.toLowerCase();

    if (s.includes('from pc_devices')) {
      return [...this.data.pc_devices];
    }

    if (s.includes('from mobile_devices where is_revoked = 0')) {
      return this.data.mobile_devices.filter((m) => m.is_revoked === 0);
    }

    if (s.includes('from device_pairings where is_active = 1')) {
      return this.data.device_pairings.filter((dp) => dp.is_active === 1);
    }

    if (s.includes('from audit_logs')) {
      return [...this.data.audit_logs].reverse().slice(0, 50);
    }

    return [];
  }

  async run(sql: string, params: any[] = []): Promise<{ lastID?: number; changes?: number }> {
    const s = sql.toLowerCase();

    if (s.includes('insert into users')) {
      const [id, email, password_hash] = params;
      this.data.users.push({ id, email, password_hash, created_at: new Date().toISOString() });
      this.persist();
      return { changes: 1 };
    }

    if (s.includes('insert into pc_devices')) {
      const [id, user_id, device_name, pc_number, pc_public_key, hardware_uuid, is_online, lock_status] = params;
      const existing = this.data.pc_devices.find(p => p.id === id || p.hardware_uuid === hardware_uuid);
      let targetPc: any;

      if (existing) {
        existing.is_online = is_online !== undefined ? is_online : 1;
        existing.last_seen_at = new Date().toISOString();
        if (device_name) existing.device_name = device_name;
        targetPc = existing;
      } else {
        const num = pc_number || `PC-0${this.data.pc_devices.length + 1}`;
        targetPc = {
          id,
          user_id: user_id || 'user_demo_1',
          device_name: device_name || `Cyber Workstation (${num})`,
          pc_number: num,
          admin_pin: '998877',
          pc_public_key: pc_public_key || 'PUBKEY',
          hardware_uuid: hardware_uuid || id,
          is_online: is_online !== undefined ? is_online : 1,
          lock_status: lock_status || 'UNLOCKED',
          last_seen_at: new Date().toISOString(),
          created_at: new Date().toISOString(),
        };
        this.data.pc_devices.push(targetPc);
      }
      this.persist();
      this.syncPcToSupabase(targetPc);
      return { changes: 1 };
    }

    if (s.includes('update pc_devices set is_online =')) {
      const is_online = params[0];
      const id = params[1];
      const pc = this.data.pc_devices.find((p) => p.id === id);
      if (pc) {
        pc.is_online = is_online;
        pc.last_seen_at = new Date().toISOString();
        this.persist();
        this.syncPcToSupabase(pc);
      }
      return { changes: 1 };
    }

    if (s.includes('update pc_devices set lock_status =')) {
      const lock_status = params[0];
      const id = params[1];
      const pc = this.data.pc_devices.find((p) => p.id === id);
      if (pc) {
        pc.lock_status = lock_status;
        pc.last_seen_at = new Date().toISOString();
        this.persist();
        this.syncPcToSupabase(pc);
      }
      return { changes: 1 };
    }

    if (s.includes('update pc_devices set admin_pin =')) {
      const admin_pin = params[0];
      const id = params[1];
      const pc = this.data.pc_devices.find((p) => p.id === id);
      if (pc) {
        pc.admin_pin = admin_pin;
        this.persist();
        this.syncPcToSupabase(pc);
      }
      return { changes: 1 };
    }

    if (s.includes('update pc_devices set device_name =')) {
      const [device_name, pc_public_key, id] = params;
      const pc = this.data.pc_devices.find((p) => p.id === id);
      if (pc) {
        pc.device_name = device_name;
        pc.pc_public_key = pc_public_key;
        pc.is_online = 1;
        pc.last_seen_at = new Date().toISOString();
        this.persist();
        this.syncPcToSupabase(pc);
      }
      return { changes: 1 };
    }

    if (s.includes('insert into mobile_devices')) {
      const [id, user_id, device_name, mobile_public_key, device_token] = params;
      const mob = {
        id,
        user_id: user_id || 'user_demo_1',
        device_name,
        mobile_public_key,
        device_token,
        is_revoked: 0,
        created_at: new Date().toISOString(),
      };
      this.data.mobile_devices.push(mob);
      this.persist();
      this.syncMobileToSupabase(mob);
      return { changes: 1 };
    }

    if (s.includes('insert or replace into device_pairings') || s.includes('insert into device_pairings')) {
      const [id, pc_id, mobile_id] = params;
      let pairing = this.data.device_pairings.find((dp) => dp.pc_id === pc_id && dp.mobile_id === mobile_id);
      if (pairing) {
        pairing.is_active = 1;
      } else {
        pairing = { id, pc_id, mobile_id, is_active: 1, paired_at: new Date().toISOString() };
        this.data.device_pairings.push(pairing);
      }
      this.persist();
      this.syncPairingToSupabase(pairing);
      return { changes: 1 };
    }

    if (s.includes('insert into audit_logs')) {
      const [id, pc_id, mobile_id, event_type, status, details] = params;
      const log = {
        id,
        pc_id,
        mobile_id: details ? mobile_id : undefined,
        event_type: details ? event_type : (params[2] || 'EVENT'),
        status: details ? status : (params[3] || 'SUCCESS'),
        details: details || params[4] || '',
        created_at: new Date().toISOString(),
      };
      this.data.audit_logs.push(log);
      this.persist();
      this.syncAuditLogToSupabase(log);
      return { changes: 1 };
    }

    return { changes: 0 };
  }

  async exec(sql: string): Promise<void> {
    // Schema initialized
  }
}

let dbInstance: Database | null = null;

export async function getDb(): Promise<Database> {
  if (!dbInstance) {
    dbInstance = new HybridSupabaseDatabase();
  }
  return dbInstance;
}
