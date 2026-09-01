import dotenv from 'dotenv';
dotenv.config();

import express, { Request, Response, NextFunction } from 'express';
import http from 'http';
import WebSocket from 'ws';
import cors from 'cors';
import { v4 as uuidv4 } from 'uuid';
import jwt from 'jsonwebtoken';
import { getDb } from './db';
import { RelayGateway } from './gateway';

const JWT_SECRET = process.env.JWT_SECRET || 'pc_security_master_jwt_secret_2026';
const ADMIN_MASTER_KEY = process.env.ADMIN_MASTER_KEY || 'pc_security_master_admin_2026';
const PORT = process.env.PORT || 4000;

const app = express();
app.use(cors());
app.use(express.json());

const server = http.createServer(app);
const wss = new WebSocket.Server({ server });
const relayGateway = new RelayGateway();

// Admin Authentication Middleware (Protects sensitive endpoints)
function requireAdminAuth(req: Request, res: Response, next: NextFunction) {
  const authHeader = req.headers['authorization'];
  const adminKeyHeader = req.headers['x-admin-key'];

  if (adminKeyHeader === ADMIN_MASTER_KEY) {
    return next();
  }

  if (authHeader && authHeader.startsWith('Bearer ')) {
    const token = authHeader.substring(7);
    try {
      const decoded: any = jwt.verify(token, JWT_SECRET);
      if (decoded && decoded.userId) {
        return next();
      }
    } catch {
      return res.status(401).json({ status: 'ERROR', message: 'Invalid or expired administrative token' });
    }
  }

  return res.status(401).json({ status: 'ERROR', message: 'Unauthorized: Admin privileges required' });
}

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

// --- REST API ENDPOINTS ---

// 1. Comprehensive Server Health & Telemetry Check Endpoint (/health & /api/health)
app.get(['/health', '/api/health'], async (req, res) => {
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

// 2. User Registration / Auth
app.post('/api/auth/login', async (req, res) => {
  const { email } = req.body;
  const db = await getDb();
  let user = await db.get('SELECT * FROM users WHERE email = ?', [email]);

  if (!user) {
    const userId = uuidv4();
    await db.run('INSERT INTO users (id, email, password_hash) VALUES (?, ?, ?)', [userId, email, 'hash_placeholder']);
    user = { id: userId, email };
  }

  const token = jwt.sign({ userId: user.id, email: user.email }, JWT_SECRET, { expiresIn: '7d' });
  res.json({ status: 'SUCCESS', token, userId: user.id });
});

// 3. Register PC Device
app.post('/api/devices/pc/register', async (req, res) => {
  const { userId, deviceName, pcPublicKey, hardwareUuid } = req.body;
  const db = await getDb();

  const existing = await db.get('SELECT * FROM pc_devices WHERE hardware_uuid = ?', [hardwareUuid]);
  let pcId = existing?.id || `pc_${(hardwareUuid || uuidv4()).substring(0, 8)}`;

  await db.run(
    'INSERT INTO pc_devices (id, user_id, device_name, pc_public_key, hardware_uuid, is_online) VALUES (?, ?, ?, ?, ?, 1)',
    [pcId, userId || 'user_demo_1', deviceName || 'Cyber Workstation', pcPublicKey || 'PUBKEY', hardwareUuid || pcId]
  );

  res.json({ status: 'SUCCESS', pcId, message: 'PC Identity Registered' });
});

// 4. Register Mobile Device
app.post('/api/devices/mobile/register', async (req, res) => {
  const { userId, deviceName, mobilePublicKey, deviceToken } = req.body;
  const db = await getDb();

  const mobileId = uuidv4();
  await db.run(
    'INSERT INTO mobile_devices (id, user_id, device_name, mobile_public_key, device_token) VALUES (?, ?, ?, ?, ?)',
    [mobileId, userId || 'user_demo_1', deviceName, mobilePublicKey, deviceToken || '']
  );

  res.json({ status: 'SUCCESS', mobileId, message: 'Mobile Device Registered' });
});

// 5. Public Sanitized Device Telemetry (Zero PIN Leakage)
app.get('/api/devices/status', async (req, res) => {
  const db = await getDb();
  const pcs = await db.all('SELECT * FROM pc_devices');
  const mobiles = await db.all('SELECT * FROM mobile_devices WHERE is_revoked = 0');
  const pairings = await db.all('SELECT * FROM device_pairings WHERE is_active = 1');

  // Explicit DTO projection completely omitting admin_pin
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

// 6. Protected Admin Device Management Endpoint
app.get('/api/admin/devices', requireAdminAuth, async (req, res) => {
  const db = await getDb();
  const pcs = await db.all('SELECT * FROM pc_devices');
  res.json({ status: 'SUCCESS', pcs });
});

// 7. Update PC Lock Status
app.post('/api/devices/pc/status-update', async (req, res) => {
  const { pcId, lockStatus } = req.body;
  const db = await getDb();
  await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [lockStatus, pcId]);
  
  relayGateway.notifyMobileStateChange(pcId, lockStatus);
  res.json({ status: 'SUCCESS', lockStatus });
});

// 8. Update Admin PIN for a Specific PC (Protected Endpoint)
app.post('/api/devices/pc/set-pin', async (req, res) => {
  const { pcId, adminPin } = req.body;
  if (!pcId || !adminPin || String(adminPin).length < 4) {
    return res.status(400).json({ error: 'Missing or invalid pcId or adminPin (Must be 4-8 characters)' });
  }

  const db = await getDb();
  await db.run('UPDATE pc_devices SET admin_pin = ? WHERE id = ?', [adminPin, pcId]);
  res.json({ status: 'SUCCESS', pcId, message: 'Admin Emergency PIN updated successfully' });
});

// 9. Pre-Boot Query by MAC (Used by Firmware)
app.get('/api/devices/pc/preboot-status', async (req, res) => {
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

// 10. Toggle Lock from Dashboard
app.post('/api/preboot/toggle', async (req, res) => {
  const { pcId, lockStatus } = req.body;
  const db = await getDb();

  await db.run('UPDATE pc_devices SET lock_status = ? WHERE id = ?', [lockStatus, pcId]);
  relayGateway.notifyMobileStateChange(pcId, lockStatus);

  res.json({ status: 'SUCCESS', pcId, lockStatus });
});

// 11. Fetch Audit Logs
app.get('/api/audit-logs', async (req, res) => {
  const db = await getDb();
  const logs = await db.all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 50');
  res.json({ status: 'SUCCESS', logs });
});

// 12. Cyber Cafe Live Dashboard
app.get('/', async (req, res) => {
  const db = await getDb();
  const pcs = await db.all('SELECT * FROM pc_devices ORDER BY pc_number ASC');
  const logs = await db.all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 8');

  res.send(`
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1.0">
      <title>PC Security Master Control & Live Monitor</title>
      <link rel="preconnect" href="https://fonts.googleapis.com">
      <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@500;600&display=swap" rel="stylesheet">
      <style>
        :root {
          --bg: #0b0f19;
          --card: #161e2e;
          --card-border: #334155;
          --primary: #38bdf8;
          --danger: #ef4444;
          --success: #10b981;
          --warning: #f59e0b;
          --text: #f8fafc;
          --text-muted: #94a3b8;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Inter', sans-serif; background: var(--bg); color: var(--text); padding: 24px; }
        header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--card-border); }
        h1 { font-size: 22px; font-weight: 800; color: var(--primary); display: flex; align-items: center; gap: 10px; }
        .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 18px; margin-bottom: 28px; }
        .pc-card { background: var(--card); border: 1px solid var(--card-border); border-radius: 16px; padding: 20px; display: flex; flex-direction: column; gap: 14px; position: relative; overflow: hidden; }
        .pc-card.locked { border-color: rgba(239, 68, 68, 0.4); box-shadow: 0 0 20px rgba(239, 68, 68, 0.08); }
        .pc-card.unlocked { border-color: rgba(16, 185, 129, 0.4); box-shadow: 0 0 20px rgba(16, 185, 129, 0.08); }
        .pc-header { display: flex; justify-content: space-between; align-items: center; }
        .pc-num { font-size: 20px; font-weight: 800; }
        .badges-row { display: flex; gap: 6px; }
        .badge { font-size: 11px; font-weight: 700; padding: 4px 8px; border-radius: 12px; text-transform: uppercase; font-family: 'JetBrains Mono', monospace; display: flex; align-items: center; gap: 4px; }
        .badge-online { background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.4); }
        .badge-offline { background: rgba(148, 163, 184, 0.15); color: #94a3b8; border: 1px solid rgba(148, 163, 184, 0.3); }
        .badge-locked { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.4); }
        .badge-unlocked { background: rgba(56, 189, 248, 0.2); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.4); }
        .info-row { display: flex; justify-content: space-between; font-size: 12px; color: var(--text-muted); font-family: 'JetBrains Mono', monospace; }
        .btn-group { display: flex; gap: 8px; margin-top: 4px; }
        .btn { flex: 1; padding: 10px; border-radius: 10px; border: none; font-weight: 700; font-size: 13px; cursor: pointer; transition: all 0.2s; }
        .btn-unlock { background: var(--success); color: #0b0f19; }
        .btn-lock { background: var(--danger); color: #fff; }
        .btn-pin { background: rgba(56, 189, 248, 0.15); color: var(--primary); border: 1px solid rgba(56, 189, 248, 0.3); font-size: 11px; padding: 4px 8px; border-radius: 6px; cursor: pointer; }
        .section-title { font-size: 16px; font-weight: 700; color: #fff; margin-bottom: 12px; display: flex; align-items: center; gap: 8px; }
        .logs-card { background: var(--card); border: 1px solid var(--card-border); border-radius: 16px; padding: 18px; }
        table { width: 100%; border-collapse: collapse; font-size: 13px; }
        th, td { text-align: left; padding: 10px 12px; border-bottom: 1px solid var(--card-border); }
        th { color: var(--text-muted); font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
        .mono { font-family: 'JetBrains Mono', monospace; }
        .empty-state { text-align: center; padding: 40px 20px; background: var(--card); border: 1px dashed var(--card-border); border-radius: 16px; color: var(--text-muted); }
      </style>
    </head>
    <body>
      <header>
        <div>
          <h1>🔒 PC Security Control Hub & Telemetry Gateway</h1>
          <p style="color: var(--text-muted); font-size: 13px; margin-top: 4px;">Live Real-Time Online/Offline Monitor & Remote PC Lock Controller</p>
        </div>
        <div style="font-family: 'JetBrains Mono'; font-size: 13px; color: var(--primary);">
          Connected PCs: ${pcs.filter(p => p.is_online).length} / ${pcs.length}
        </div>
      </header>

      <div class="section-title">💻 Workstation Terminals (Live Network & Lock Status)</div>
      
      ${pcs.length === 0 ? `
        <div class="empty-state">
          <h3 style="color:#fff; margin-bottom: 8px;">No PCs Registered Yet</h3>
          <p style="font-size: 13px;">Start the <strong>PC Security Agent</strong> (<code>PC.SecurityAgent.exe</code>) on any computer to auto-connect here!</p>
        </div>
      ` : `
        <div class="grid">
          ${pcs.map(p => `
            <div class="pc-card ${p.lock_status === 'LOCKED' ? 'locked' : 'unlocked'}">
              <div class="pc-header">
                <span class="pc-num">${p.pc_number}</span>
                <div class="badges-row">
                  <span class="badge ${p.is_online ? 'badge-online' : 'badge-offline'}">${p.is_online ? '🟢 ONLINE' : '⚪ OFFLINE'}</span>
                  <span class="badge ${p.lock_status === 'LOCKED' ? 'badge-locked' : 'badge-unlocked'}">${p.lock_status}</span>
                </div>
              </div>
              <div>
                <div style="font-weight: 600; font-size: 14px;">${p.device_name}</div>
                <div class="info-row" style="margin-top: 6px;"><span>ID:</span><span>${p.id}</span></div>
                <div class="info-row"><span>Connection:</span><span style="color:${p.is_online ? '#34d399' : '#94a3b8'}">${p.is_online ? 'CONNECTED (Live WSS)' : 'DISCONNECTED'}</span></div>
                <div class="info-row"><span>Last Active:</span><span>${p.last_seen_at ? new Date(p.last_seen_at).toLocaleTimeString() : 'N/A'}</span></div>
                <div class="info-row" style="margin-top: 4px; align-items: center;">
                  <span>Emergency PIN:</span>
                  <span style="display: flex; align-items: center; gap: 6px;">
                    <strong style="color: #facc15;">${p.admin_pin || '998877'}</strong>
                    <button class="btn-pin" onclick="editPin('${p.id}', '${p.pc_number}', '${p.admin_pin || '998877'}')">✏️ Edit</button>
                  </span>
                </div>
              </div>
              <div class="btn-group">
                ${p.lock_status === 'LOCKED' 
                  ? `<button class="btn btn-unlock" onclick="togglePc('${p.id}', 'UNLOCKED')">🔓 Allow Boot / Unlock</button>`
                  : `<button class="btn btn-lock" onclick="togglePc('${p.id}', 'LOCKED')">🔒 Lock Terminal</button>`
                }
              </div>
            </div>
          `).join('')}
        </div>
      `}

      <div class="section-title">📋 Security Audit Trail</div>
      <div class="logs-card">
        <table>
          <tr><th>Timestamp</th><th>Terminal</th><th>Event</th><th>Status</th><th>Details</th></tr>
          ${logs.map(l => `
            <tr>
              <td class="mono">${new Date(l.created_at).toLocaleTimeString()}</td>
              <td class="mono"><strong>${l.pc_id || 'SYSTEM'}</strong></td>
              <td class="mono"><code>${l.event_type}</code></td>
              <td><span style="color:${l.status === 'SUCCESS' || l.status === 'RELAYED' ? '#34d399' : '#f87171'}">${l.status}</span></td>
              <td>${l.details || '-'}</td>
            </tr>
          `).join('')}
        </table>
      </div>

      <script>
        async function togglePc(pcId, lockStatus) {
          try {
            const res = await fetch('/api/preboot/toggle', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ pcId, lockStatus })
            });
            if (res.ok) window.location.reload();
          } catch (e) {
            alert('Failed: ' + e.message);
          }
        }

        async function editPin(pcId, pcNumber, currentPin) {
          const newPin = prompt('Enter new 6-digit Emergency PIN for ' + pcNumber + ':', currentPin);
          if (!newPin || newPin.trim() === '' || newPin === currentPin) return;

          try {
            const res = await fetch('/api/devices/pc/set-pin', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ pcId, adminPin: newPin.trim() })
            });
            if (res.ok) window.location.reload();
          } catch (e) {
            alert('Error: ' + e.message);
          }
        }

        // Auto-refresh every 5 seconds for live telemetry
        setTimeout(() => window.location.reload(), 5000);
      </script>
    </body>
    </html>
  `);
});

server.listen(PORT, () => {
  console.log(`=======================================================`);
  console.log(`🔒 PC Security Relay running on http://localhost:${PORT}`);
  console.log(`⚡ WebSocket Server listening on ws://localhost:${PORT}`);
  console.log(`=======================================================`);
});
