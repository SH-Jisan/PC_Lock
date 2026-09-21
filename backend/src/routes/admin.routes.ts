import { Router } from 'express';
import { getDb } from '../db';
import { requireAdminAuth } from '../middleware/auth.middleware';
import { RelayGateway } from '../gateway';

export function createAdminRouter(relayGateway: RelayGateway): Router {
  const router = Router();

  // Protected Admin Device Management Endpoint
  router.get('/devices', requireAdminAuth, async (req, res) => {
    const db = await getDb();
    const pcs = await db.all('SELECT * FROM pc_devices');
    res.json({ status: 'SUCCESS', pcs });
  });

  return router;
}

export function createAuditLogsRouter(): Router {
  const router = Router();

  router.get('/', async (req, res) => {
    const db = await getDb();
    const logs = await db.all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 50');
    res.json({ status: 'SUCCESS', logs });
  });

  return router;
}
