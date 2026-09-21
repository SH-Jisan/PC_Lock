import fs from 'fs';
import path from 'path';
import { DbSchema } from './types';

export class LocalStore {
  private dbPath: string;
  private saveTimeout: NodeJS.Timeout | null = null;

  constructor() {
    this.dbPath = path.resolve(__dirname, '../../security_relay.json');
  }

  public loadData(): DbSchema {
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

  public persist(data: DbSchema) {
    if (this.saveTimeout) clearTimeout(this.saveTimeout);
    this.saveTimeout = setTimeout(() => {
      try {
        fs.writeFileSync(this.dbPath, JSON.stringify(data, null, 2), 'utf-8');
      } catch (err: any) {
        console.error('[DB Persist Error]:', err.message);
      }
    }, 50);
  }
}
