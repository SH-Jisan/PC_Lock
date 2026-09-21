import dotenv from 'dotenv';
dotenv.config();

import express from 'express';
import http from 'http';
import WebSocket from 'ws';
import cors from 'cors';
import jwt from 'jsonwebtoken';
import { getDb } from './db';
import { RelayGateway } from './gateway';
import { renderDashboardHtml } from './views/dashboard.view';
import { createHealthRouter } from './routes/health.routes';
import { createAuthRouter } from './routes/auth.routes';
import { createDevicesRouter } from './routes/devices.routes';
import { createAdminRouter, createAuditLogsRouter } from './routes/admin.routes';

const JWT_SECRET = process.env.JWT_SECRET || 'pc_security_master_jwt_secret_2026';
const PORT = process.env.PORT || 4000;

const app = express();
app.use(cors());
app.use(express.json());

const server = http.createServer(app);
const wss = new WebSocket.Server({ server });
const relayGateway = new RelayGateway();

// WebSocket Connection Router
wss.on('connection', (ws: WebSocket, req: http.IncomingMessage) => {
  const urlParams = new URLSearchParams(req.url?.split('?')[1] || '');
  const deviceId = urlParams.get('device_id');
  const deviceType = urlParams.get('device_type') as 'PC' | 'MOBILE';
  const token = urlParams.get('token');

  if (!deviceId || !deviceType) {
    ws.close(4001, 'Missing device_id or device_type');
    return;
  }

  let userId = 'user_demo_1';
  if (token) {
    try {
      const decoded: any = jwt.verify(token, JWT_SECRET);
      userId = decoded.userId;
    } catch {}
  }

  relayGateway.handleConnection(ws, deviceId, deviceType, userId);
});

// Mount Routes
app.use('/health', createHealthRouter(relayGateway));
app.use('/api/health', createHealthRouter(relayGateway));
app.use('/api/auth', createAuthRouter());
app.use('/api/devices', createDevicesRouter(relayGateway));
app.use('/api/admin', createAdminRouter(relayGateway));
app.use('/api/audit-logs', createAuditLogsRouter());

// Toggle Lock from Dashboard
app.post('/api/preboot/toggle', async (req, res) => {
  const { pcId, lockStatus } = req.body;
  const db = await getDb();

  await db.run('UPDATE pc_devices SET lock_status = ? WHERE id = ?', [lockStatus, pcId]);
  relayGateway.notifyMobileStateChange(pcId, lockStatus);

  res.json({ status: 'SUCCESS', pcId, lockStatus });
});

// Cyber Cafe Live Dashboard
app.get('/', async (req, res) => {
  const db = await getDb();
  const pcs = await db.all('SELECT * FROM pc_devices ORDER BY pc_number ASC');
  const logs = await db.all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 8');

  res.send(renderDashboardHtml(pcs, logs));
});

server.listen(PORT, () => {
  console.log(`=======================================================`);
  console.log(`🔒 PC Security Relay running on http://localhost:${PORT}`);
  console.log(`⚡ WebSocket Server listening on ws://localhost:${PORT}`);
  console.log(`=======================================================`);
});
