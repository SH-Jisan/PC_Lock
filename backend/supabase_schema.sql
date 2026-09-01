-- ====================================================================
-- PC Security Remote Lock & Telemetry System - Supabase Schema
-- ====================================================================

-- 1. PC Devices Table
CREATE TABLE IF NOT EXISTS pc_devices (
    id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL DEFAULT 'user_demo_1',
    device_name TEXT NOT NULL,
    pc_number TEXT,
    mac_address TEXT,
    admin_pin TEXT DEFAULT '998877',
    pc_public_key TEXT NOT NULL,
    hardware_uuid TEXT UNIQUE NOT NULL,
    is_online BOOLEAN DEFAULT false,
    lock_status TEXT DEFAULT 'UNLOCKED',
    last_seen_at TIMESTAMPTZ DEFAULT NOW(),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 2. Mobile Controller Devices Table
CREATE TABLE IF NOT EXISTS mobile_devices (
    id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL DEFAULT 'user_demo_1',
    device_name TEXT NOT NULL,
    mobile_public_key TEXT NOT NULL,
    device_token TEXT,
    is_revoked BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 3. Device Pairings Table (Mobile <-> PC Trust Relationships)
CREATE TABLE IF NOT EXISTS device_pairings (
    id TEXT PRIMARY KEY,
    pc_id TEXT REFERENCES pc_devices(id) ON DELETE CASCADE,
    mobile_id TEXT REFERENCES mobile_devices(id) ON DELETE CASCADE,
    is_active BOOLEAN DEFAULT true,
    paired_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(pc_id, mobile_id)
);

-- 4. Security Audit Trail Logs Table
CREATE TABLE IF NOT EXISTS audit_logs (
    id TEXT PRIMARY KEY,
    pc_id TEXT,
    mobile_id TEXT,
    event_type TEXT NOT NULL,
    status TEXT NOT NULL,
    details TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 5. Enable Realtime Replication for Instant Push
ALTER PUBLICATION supabase_realtime ADD TABLE pc_devices;
ALTER PUBLICATION supabase_realtime ADD TABLE audit_logs;

-- Indices for Fast Querying
CREATE INDEX IF NOT EXISTS idx_pc_devices_online ON pc_devices(is_online);
CREATE INDEX IF NOT EXISTS idx_pc_devices_uuid ON pc_devices(hardware_uuid);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created ON audit_logs(created_at DESC);
