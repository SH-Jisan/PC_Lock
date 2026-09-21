import { Router } from 'express';
import { getDb } from '../db';
import { RelayGateway } from '../gateway';

export function createHealthRouter(relayGateway: RelayGateway): Router {
  const router = Router();

  router.get(['/', '/api/health', '/health'], async (req, res) => {
    try {
      const db = await getDb();
      const pcCount = await db.get('SELECT COUNT(*) as cnt FROM pc_devices');
      const stats = relayGateway.getConnectedStats();
      const uptimeSeconds = Math.floor(process.uptime());
      const mem = process.memoryUsage();
      const supabaseInfo = db.getSupabaseStatus();

      const hours = Math.floor(uptimeSeconds / 3600);
      const minutes = Math.floor((uptimeSeconds % 3600) / 60);
      const seconds = uptimeSeconds % 60;

      res.status(200).json({
        status: 'UP',
        health: 'HEALTHY',
        service: 'PC Remote Security & Pre-Boot Control Gateway',
        version: '2.0.0',
        uptime: {
          seconds: uptimeSeconds,
          human: `${hours}h ${minutes}m ${seconds}s`,
        },
        server_time: new Date().toISOString(),
        live_connections: {
          connected_terminals: stats.onlinePcs,
          connected_mobile_controllers: stats.onlineMobiles,
          total_active_websockets: stats.totalSockets,
        },
        database: {
          status: 'CONNECTED',
          registered_workstations: pcCount?.cnt || 0,
          supabase_cloud: supabaseInfo,
        },
        system: {
          platform: process.platform,
          node_version: process.version,
          memory_heap_used_mb: Number((mem.heapUsed / 1024 / 1024).toFixed(2)),
          memory_rss_mb: Number((mem.rss / 1024 / 1024).toFixed(2)),
        },
      });
    } catch (error: any) {
      res.status(503).json({
        status: 'DOWN',
        health: 'UNHEALTHY',
        error: error.message,
        server_time: new Date().toISOString(),
      });
    }
  });

  return router;
}
