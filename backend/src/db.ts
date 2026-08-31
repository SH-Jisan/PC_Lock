import sqlite3 from 'sqlite3';
import { open, Database } from 'sqlite';
import path from 'path';

let dbInstance: Database | null = null;

export async function getDb(): Promise<Database> {
  if (dbInstance) return dbInstance;

  const dbPath = path.join(__dirname, '../security_relay.db');
  dbInstance = await open({
    filename: dbPath,
    driver: sqlite3.Database,
  });

  await initDbSchema(dbInstance);
  return dbInstance;
}

async function initDbSchema(db: Database) {
  await db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id TEXT PRIMARY KEY,
      email TEXT UNIQUE NOT NULL,
      password_hash TEXT NOT NULL,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP
    );

    CREATE TABLE IF NOT EXISTS pc_devices (
      id TEXT PRIMARY KEY,
      user_id TEXT NOT NULL,
      device_name TEXT NOT NULL,
      pc_number TEXT DEFAULT 'PC-01',
      mac_address TEXT,
      admin_pin TEXT DEFAULT '998877',
      pc_public_key TEXT NOT NULL,
      hardware_uuid TEXT UNIQUE NOT NULL,
      is_online INTEGER DEFAULT 0,
      lock_status TEXT DEFAULT 'LOCKED',
      last_seen_at DATETIME,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS mobile_devices (
      id TEXT PRIMARY KEY,
      user_id TEXT NOT NULL,
      device_name TEXT NOT NULL,
      mobile_public_key TEXT NOT NULL,
      device_token TEXT,
      is_revoked INTEGER DEFAULT 0,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS device_pairings (
      id TEXT PRIMARY KEY,
      pc_id TEXT NOT NULL,
      mobile_id TEXT NOT NULL,
      paired_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      is_active INTEGER DEFAULT 1,
      UNIQUE(pc_id, mobile_id),
      FOREIGN KEY (pc_id) REFERENCES pc_devices(id) ON DELETE CASCADE,
      FOREIGN KEY (mobile_id) REFERENCES mobile_devices(id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS audit_logs (
      id TEXT PRIMARY KEY,
      pc_id TEXT,
      mobile_id TEXT,
      event_type TEXT NOT NULL,
      status TEXT NOT NULL,
      ip_address TEXT,
      details TEXT,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP
    );
  `);

  // Ensure admin_pin column exists for existing databases
  try {
    await db.exec(`ALTER TABLE pc_devices ADD COLUMN admin_pin TEXT DEFAULT '998877'`);
  } catch {}

  // Pre-seed sample Cyber Cafe terminals if empty
  const count = await db.get('SELECT COUNT(*) as cnt FROM pc_devices');
  if (count && count.cnt === 0) {
    await db.exec(`
      INSERT INTO pc_devices (id, user_id, device_name, pc_number, mac_address, admin_pin, pc_public_key, hardware_uuid, is_online, lock_status)
      VALUES 
        ('pc_dev_01', 'user_demo_1', 'Cyber Gaming Terminal 1', 'PC-01', 'AA:BB:CC:DD:EE:01', '123456', 'PUBKEY_01', 'HW_UUID_01', 1, 'LOCKED'),
        ('pc_dev_02', 'user_demo_1', 'Cyber Gaming Terminal 2', 'PC-02', 'AA:BB:CC:DD:EE:02', '654321', 'PUBKEY_02', 'HW_UUID_02', 0, 'LOCKED'),
        ('pc_dev_03', 'user_demo_1', 'Cyber Gaming Terminal 3', 'PC-03', 'AA:BB:CC:DD:EE:03', '998877', 'PUBKEY_03', 'HW_UUID_03', 1, 'UNLOCKED'),
        ('pc_dev_04', 'user_demo_1', 'Cyber Gaming Terminal 4', 'PC-04', 'AA:BB:CC:DD:EE:04', '778899', 'PUBKEY_04', 'HW_UUID_04', 0, 'LOCKED');

      INSERT OR IGNORE INTO mobile_devices (id, user_id, device_name, mobile_public_key, is_revoked)
      VALUES ('mob_dev_8f7a1c', 'user_demo_1', 'Admin Master Controller Phone', '3b7f8c9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e', 0);

      INSERT OR IGNORE INTO device_pairings (id, pc_id, mobile_id, is_active)
      VALUES 
        ('pair_01', 'pc_dev_01', 'mob_dev_8f7a1c', 1),
        ('pair_02', 'pc_dev_02', 'mob_dev_8f7a1c', 1),
        ('pair_03', 'pc_dev_03', 'mob_dev_8f7a1c', 1),
        ('pair_04', 'pc_dev_04', 'mob_dev_8f7a1c', 1);
    `);
  }

  console.log('[DB] Database schema initialized with Custom Admin PIN & Pairing support.');
}



