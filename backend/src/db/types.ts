export interface Database {
  get(sql: string, params?: any[]): Promise<any>;
  all(sql: string, params?: any[]): Promise<any[]>;
  run(sql: string, params?: any[]): Promise<{ lastID?: number; changes?: number }>;
  exec(sql: string): Promise<void>;
  getSupabaseStatus(): { configured: boolean; status: string; url?: string; project_ref?: string };
  deletePcDevice(pcId: string): Promise<void>;
}

export interface UserEntity {
  id: string;
  email: string;
  password_hash: string;
  created_at: string;
}

export interface PcDeviceEntity {
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
}

export interface MobileDeviceEntity {
  id: string;
  user_id: string;
  device_name: string;
  mobile_public_key: string;
  device_token?: string;
  is_revoked: number;
  created_at: string;
}

export interface DevicePairingEntity {
  id: string;
  pc_id: string;
  mobile_id: string;
  is_active: number;
  paired_at: string;
}

export interface AuditLogEntity {
  id: string;
  pc_id?: string;
  mobile_id?: string;
  event_type: string;
  status: string;
  details?: string;
  created_at: string;
}

export interface DbSchema {
  users: Array<UserEntity>;
  pc_devices: Array<PcDeviceEntity>;
  mobile_devices: Array<MobileDeviceEntity>;
  device_pairings: Array<DevicePairingEntity>;
  audit_logs: Array<AuditLogEntity>;
}
