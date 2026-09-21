import { Router } from 'express';
import { v4 as uuidv4 } from 'uuid';
import { getDb } from '../db';
import { RelayGateway } from '../gateway';

export function createDevicesRouter(relayGateway: RelayGateway): Router {
  const router = Router();

  // Register PC Device
  router.post('/pc/register', async (req, res) => {
    const { userId, deviceName, pcPublicKey, hardwareUuid } = req.body;
    const db = await getDb();

    const existing = await db.get('SELECT * FROM pc_devices WHERE hardware_uuid = ?', [hardwareUuid]);
    const pcId = existing?.id || `pc_${(hardwareUuid || uuidv4()).substring(0, 8)}`;

    await db.run(
      'INSERT INTO pc_devices (id, user_id, device_name, pc_public_key, hardware_uuid, is_online) VALUES (?, ?, ?, ?, ?, 1)',
      [pcId, userId || 'user_demo_1', deviceName || 'Cyber Workstation', pcPublicKey || 'PUBKEY', hardwareUuid || pcId]
    );

    res.json({ status: 'SUCCESS', pcId, message: 'PC Identity Registered' });
  });

  // Register Mobile Device
  router.post('/mobile/register', async (req, res) => {
    const { userId, deviceName, mobilePublicKey, deviceToken } = req.body;
    const db = await getDb();

    const mobileId = uuidv4();
    await db.run(
      'INSERT INTO mobile_devices (id, user_id, device_name, mobile_public_key, device_token) VALUES (?, ?, ?, ?, ?)',
      [mobileId, userId || 'user_demo_1', deviceName, mobilePublicKey, deviceToken || '']
    );

    res.json({ status: 'SUCCESS', mobileId, message: 'Mobile Device Registered' });
  });

  // Public Sanitized Device Telemetry (Zero PIN Leakage)
  router.get('/status', async (req, res) => {
    const db = await getDb();
    const pcs = await db.all('SELECT * FROM pc_devices');
    const mobiles = await db.all('SELECT * FROM mobile_devices WHERE is_revoked = 0');
    const pairings = await db.all('SELECT * FROM device_pairings WHERE is_active = 1');

    const sanitizedPcs = pcs.map((p) => ({
      id: p.id,
      user_id: p.user_id,
      device_name: p.device_name,
      pc_number: p.pc_number,
      mac_address: p.mac_address,
      is_online: p.is_online,
      lock_status: p.lock_status,
      last_seen_at: p.last_seen_at,
      created_at: p.created_at,
    }));

    res.json({
      status: 'SUCCESS',
      pcs: sanitizedPcs,
      mobiles,
      pairings,
      server_time: new Date().toISOString(),
    });
  });

  // Update PC Lock Status (Telemetry Report from PC Agent)
  router.post('/pc/status-update', async (req, res) => {
    const { pcId, lockStatus } = req.body;
    const db = await getDb();
    await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [lockStatus, pcId]);

    relayGateway.notifyMobileStateChange(pcId, lockStatus);
    res.json({ status: 'SUCCESS', lockStatus });
  });

  // Deregister & Completely Purge PC from Supabase Cloud & Local Relay
  router.post('/pc/deregister', async (req, res) => {
    const { pcId, hardwareUuid } = req.body;
    const db = await getDb();

    let targetId = pcId;
    if (!targetId && hardwareUuid) {
      const pc = await db.get('SELECT * FROM pc_devices WHERE hardware_uuid = ? OR id = ?', [hardwareUuid, hardwareUuid]);
      targetId = pc?.id;
    }

    if (!targetId) {
      return res.status(400).json({ status: 'ERROR', message: 'Missing pcId or hardwareUuid' });
    }

    await db.deletePcDevice(targetId);
    relayGateway.notifyPcDeregistered(targetId);

    res.json({
      status: 'SUCCESS',
      pcId: targetId,
      message: `PC ${targetId} completely purged from Supabase cloud database and relay store.`
    });
  });

  // Update Admin PIN for a Specific PC
  router.post('/pc/set-pin', async (req, res) => {
    const { pcId, adminPin } = req.body;
    if (!pcId || !adminPin || String(adminPin).length < 4) {
      return res.status(400).json({ error: 'Missing or invalid pcId or adminPin (Must be 4-8 characters)' });
    }

    const db = await getDb();
    await db.run('UPDATE pc_devices SET admin_pin = ? WHERE id = ?', [adminPin, pcId]);
    res.json({ status: 'SUCCESS', pcId, message: 'Admin Emergency PIN updated successfully' });
  });

  // Pre-Boot Query by MAC (Used by Firmware)
  router.get('/pc/preboot-status', async (req, res) => {
    const mac = (req.query.mac as string || '').toUpperCase();
    const db = await getDb();
    const pc = await db.get('SELECT id, lock_status, admin_pin FROM pc_devices WHERE UPPER(mac_address) = ?', [mac]);

    if (!pc) {
      return res.json({ lock_status: 'LOCKED', message: 'Workstation unassigned' });
    }

    res.json({
      pc_id: pc.id,
      lock_status: pc.lock_status || 'LOCKED',
      timestamp: Math.floor(Date.now() / 1000),
    });
  });

  return router;
}
