import dotenv from 'dotenv';
dotenv.config();

import { Database, DbSchema, PcDeviceEntity, MobileDeviceEntity, DevicePairingEntity, AuditLogEntity } from './db/types';
import { LocalStore } from './db/localStore';
import { SupabaseStore } from './db/supabaseStore';

export * from './db/types';

class HybridSupabaseDatabase implements Database {
  private localStore: LocalStore;
  private supabaseStore: SupabaseStore;
  private data: DbSchema;

  constructor() {
    this.localStore = new LocalStore();
    this.supabaseStore = new SupabaseStore();
    this.data = this.localStore.loadData();
    this.initDefaultSeed();
    this.supabaseStore.hydrate(this.data, () => this.localStore.persist(this.data));
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
      this.localStore.persist(this.data);
    }
  }

  public getSupabaseStatus(): { configured: boolean; status: string; url?: string; project_ref?: string } {
    return this.supabaseStore.getStatus();
  }

  public async deletePcDevice(pcId: string): Promise<void> {
    this.data.pc_devices = this.data.pc_devices.filter((p) => p.id !== pcId);
    this.data.device_pairings = this.data.device_pairings.filter((p) => p.pc_id !== pcId);
    this.localStore.persist(this.data);
    await this.supabaseStore.purgePcDevice(pcId);
  }

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
      this.localStore.persist(this.data);
      return { changes: 1 };
    }

    if (s.includes('insert into pc_devices')) {
      const [id, user_id, device_name, pc_number, pc_public_key, hardware_uuid, is_online, lock_status] = params;
      const existing = this.data.pc_devices.find((p) => p.id === id || p.hardware_uuid === hardware_uuid);
      let targetPc: PcDeviceEntity;

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
      this.localStore.persist(this.data);
      this.supabaseStore.syncPc(targetPc);
      return { changes: 1 };
    }

    if (s.includes('update pc_devices set is_online =')) {
      const is_online = params[0];
      const id = params[1];
      const pc = this.data.pc_devices.find((p) => p.id === id);
      if (pc) {
        pc.is_online = is_online;
        pc.last_seen_at = new Date().toISOString();
        this.localStore.persist(this.data);
        this.supabaseStore.syncPc(pc);
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
        this.localStore.persist(this.data);
        this.supabaseStore.syncPc(pc);
      }
      return { changes: 1 };
    }

    if (s.includes('update pc_devices set admin_pin =')) {
      const admin_pin = params[0];
      const id = params[1];
      const pc = this.data.pc_devices.find((p) => p.id === id);
      if (pc) {
        pc.admin_pin = admin_pin;
        this.localStore.persist(this.data);
        this.supabaseStore.syncPc(pc);
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
        this.localStore.persist(this.data);
        this.supabaseStore.syncPc(pc);
      }
      return { changes: 1 };
    }

    if (s.includes('insert into mobile_devices')) {
      const [id, user_id, device_name, mobile_public_key, device_token] = params;
      const mob: MobileDeviceEntity = {
        id,
        user_id: user_id || 'user_demo_1',
        device_name,
        mobile_public_key,
        device_token,
        is_revoked: 0,
        created_at: new Date().toISOString(),
      };
      this.data.mobile_devices.push(mob);
      this.localStore.persist(this.data);
      this.supabaseStore.syncMobile(mob);
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
      this.localStore.persist(this.data);
      this.supabaseStore.syncPairing(pairing);
      return { changes: 1 };
    }

    if (s.includes('insert into audit_logs')) {
      const [id, pc_id, mobile_id, event_type, status, details] = params;
      const log: AuditLogEntity = {
        id,
        pc_id,
        mobile_id: details ? mobile_id : undefined,
        event_type: details ? event_type : (params[2] || 'EVENT'),
        status: details ? status : (params[3] || 'SUCCESS'),
        details: details || params[4] || '',
        created_at: new Date().toISOString(),
      };
      this.data.audit_logs.push(log);
      this.localStore.persist(this.data);
      this.supabaseStore.syncAuditLog(log);
      return { changes: 1 };
    }

    return { changes: 0 };
  }

  async exec(sql: string): Promise<void> {
    // Schema is initialized in-memory
  }
}

let dbInstance: Database | null = null;

export async function getDb(): Promise<Database> {
  if (!dbInstance) {
    dbInstance = new HybridSupabaseDatabase();
  }
  return dbInstance;
}
