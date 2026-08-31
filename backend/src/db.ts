import fs from 'fs';
import path from 'path';

export interface Database {
  get(sql: string, params?: any[]): Promise<any>;
  all(sql: string, params?: any[]): Promise<any[]>;
  run(sql: string, params?: any[]): Promise<{ lastID?: number; changes?: number }>;
  exec(sql: string): Promise<void>;
}

interface DbStore {
  users: Array<{ id: string; email: string; password_hash: string; created_at: string }>;
  pc_devices: Array<{
    id: string;
    user_id: string;
    device_name: string;
    pc_number: string;
    mac_address?: string;
    admin_pin: string;
    pc_public_key: string;
    hardware_uuid: string;
    is_online: number;
    lock_status: string;
    last_seen_at?: string;
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
    paired_at: string;
    is_active: number;
  }>;
  audit_logs: Array<{
    id: string;
    pc_id?: string;
    mobile_id?: string;
    event_type: string;
    status: string;
    ip_address?: string;
    details?: string;
    created_at: string;
  }>;
}

class JsonDatabase implements Database {
  private dbPath: string;
  private data: DbStore;
  private saveTimeout: NodeJS.Timeout | null = null;

  constructor() {
    this.dbPath = path.join(__dirname, '../security_relay.json');
    this.data = this.loadData();
    this.initDefaultSeed();
  }

  private loadData(): DbStore {
    try {
      if (fs.existsSync(this.dbPath)) {
        const raw = fs.readFileSync(this.dbPath, 'utf-8');
        return JSON.parse(raw);
      }
    } catch (e) {
      console.warn('[DB] Initializing new database store...');
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
    // Zero predefined PCs: Only real connected PCs will appear
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
      if (existing) {
        existing.is_online = is_online !== undefined ? is_online : 1;
        existing.last_seen_at = new Date().toISOString();
        if (device_name) existing.device_name = device_name;
      } else {
        const num = pc_number || `PC-0${this.data.pc_devices.length + 1}`;
        this.data.pc_devices.push({
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
        });
      }
      this.persist();
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
      }
      return { changes: 1 };
    }

    if (s.includes('insert into mobile_devices')) {
      const [id, user_id, device_name, mobile_public_key, device_token] = params;
      this.data.mobile_devices.push({
        id,
        user_id,
        device_name,
        mobile_public_key,
        device_token,
        is_revoked: 0,
        created_at: new Date().toISOString(),
      });
      this.persist();
      return { changes: 1 };
    }

    if (s.includes('insert or replace into device_pairings') || s.includes('insert into device_pairings')) {
      const [id, pc_id, mobile_id] = params;
      const existing = this.data.device_pairings.find((dp) => dp.pc_id === pc_id && dp.mobile_id === mobile_id);
      if (existing) {
        existing.is_active = 1;
      } else {
        this.data.device_pairings.push({ id, pc_id, mobile_id, is_active: 1, paired_at: new Date().toISOString() });
      }
      this.persist();
      return { changes: 1 };
    }

    if (s.includes('insert into audit_logs')) {
      const [id, pc_id, mobile_id, event_type, status, details] = params;
      this.data.audit_logs.push({
        id,
        pc_id,
        mobile_id: details ? mobile_id : undefined,
        event_type: details ? event_type : (params[2] || 'EVENT'),
        status: details ? status : (params[3] || 'SUCCESS'),
        details: details || params[4] || '',
        created_at: new Date().toISOString(),
      });
      this.persist();
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
    dbInstance = new JsonDatabase();
    console.log('[DB] High-Performance Clean Database Engine Initialized (0 Predefined PCs).');
  }
  return dbInstance;
}
