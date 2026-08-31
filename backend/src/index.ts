import express from 'express';
import http from 'http';
import WebSocket from 'ws';
import cors from 'cors';
import { v4 as uuidv4 } from 'uuid';
import jwt from 'jsonwebtoken';
import { getDb } from './db';
import { RelayGateway } from './gateway';

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

  // Token validation (optional bypass for demo PC agent)
  let userId = 'user_demo_1';
  if (token) {
    try {
      const decoded: any = jwt.verify(token, JWT_SECRET);
      userId = decoded.userId;
    } catch {
      console.warn('[WSS] Invalid token, proceeding with guest mode for testing');
    }
  }

  relayGateway.handleConnection(ws, deviceId, deviceType, userId);
});

// --- REST API ENDPOINTS ---

// 1. Health check
app.get('/api/health', (req, res) => {
  res.json({ status: 'HEALTHY', timestamp: new Date().toISOString(), service: 'PC Security Relay' });
});

// 2. User Registration / Auth
app.post('/api/auth/login', async (req, res) => {
  const { email, password } = req.body;
  const db = await getDb();
  let user = await db.get('SELECT * FROM users WHERE email = ?', [email]);

  if (!user) {
    // Auto-create user for frictionless onboarding demo
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
  let pcId = existing?.id || uuidv4();

  if (existing) {
    await db.run('UPDATE pc_devices SET device_name = ?, pc_public_key = ?, is_online = 1 WHERE id = ?', [
      deviceName,
      pcPublicKey,
      pcId,
    ]);
  } else {
    await db.run(
      'INSERT INTO pc_devices (id, user_id, device_name, pc_public_key, hardware_uuid, is_online) VALUES (?, ?, ?, ?, ?, 1)',
      [pcId, userId || 'user_demo_1', deviceName, pcPublicKey, hardwareUuid]
    );
  }

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

// 5. Initiate Device Pairing (QR Payload)
app.post('/api/pairing/initiate', async (req, res) => {
  const { pcId } = req.body;
  const db = await getDb();
  const pc = await db.get('SELECT * FROM pc_devices WHERE id = ?', [pcId]);

  if (!pc) return res.status(404).json({ error: 'PC not found' });

  const pairingSecret = uuidv4();
  const qrPayload = JSON.stringify({
    pc_id: pc.id,
    pc_name: pc.device_name,
    public_key: pc.pc_public_key,
    secret: pairingSecret,
    relay_url: `ws://localhost:${PORT}`,
  });

  res.json({ status: 'SUCCESS', qrPayload, pairingSecret });
});

// 6. Confirm Device Pairing
app.post('/api/pairing/confirm', async (req, res) => {
  const { pcId, mobileId } = req.body;
  const db = await getDb();

  const pairingId = uuidv4();
  await db.run(
    'INSERT OR REPLACE INTO device_pairings (id, pc_id, mobile_id, is_active) VALUES (?, ?, ?, 1)',
    [pairingId, pcId, mobileId]
  );

  res.json({ status: 'SUCCESS', pairingId, message: 'Pairing Established!' });
});

// 7. Get Device Status
app.get('/api/devices/status', async (req, res) => {
  const db = await getDb();
  const pcs = await db.all('SELECT * FROM pc_devices');
  const mobiles = await db.all('SELECT * FROM mobile_devices WHERE is_revoked = 0');
  const pairings = await db.all('SELECT * FROM device_pairings WHERE is_active = 1');

  res.json({ status: 'SUCCESS', pcs, mobiles, pairings });
});

// 8. Update PC Lock Status (Called by PC Agent when state changes)
app.post('/api/devices/pc/status-update', async (req, res) => {
  const { pcId, lockStatus } = req.body;
  const db = await getDb();
  await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [lockStatus, pcId]);
  
  relayGateway.notifyMobileStateChange(pcId, lockStatus);
  res.json({ status: 'SUCCESS', lockStatus });
});

// 9. Pre-Boot Status Check (Queried by Custom UEFI Application before OS boot)
app.get('/api/preboot/status', async (req, res) => {
  const mac = (req.query.mac as string)?.toUpperCase();
  const pcId = req.query.pc_id as string;
  const db = await getDb();

  let pc = null;
  if (mac) {
    pc = await db.get('SELECT * FROM pc_devices WHERE UPPER(mac_address) = ?', [mac]);
  }
  if (!pc && pcId) {
    pc = await db.get('SELECT * FROM pc_devices WHERE id = ?', [pcId]);
  }
  if (!pc) {
    // Default fallback to first registered terminal or default state
    pc = await db.get('SELECT * FROM pc_devices ORDER BY pc_number ASC LIMIT 1');
  }

  const isLocked = pc ? pc.lock_status === 'LOCKED' : true;

  res.json({
    status: 'SUCCESS',
    preboot_authorized: !isLocked,
    lock_status: pc?.lock_status || 'LOCKED',
    pc_number: pc?.pc_number || 'PC-01',
    device_name: pc?.device_name || 'Cyber Station',
    mac_address: pc?.mac_address || mac || 'UNKNOWN',
    admin_pin: pc?.admin_pin || '998877',
    message: isLocked 
      ? 'TERMINAL LOCKED - Unlock from Cyber Cafe Reception Counter to Boot Windows'
      : 'TERMINAL UNLOCKED - Booting Windows OS'
  });
});

// 10. Update Admin PIN for a Specific PC (From Mobile App or Counter Dashboard)
app.post('/api/devices/pc/set-pin', async (req, res) => {
  const { pcId, adminPin } = req.body;
  if (!pcId || !adminPin) {
    return res.status(400).json({ error: 'Missing pcId or adminPin' });
  }

  const db = await getDb();
  await db.run('UPDATE pc_devices SET admin_pin = ? WHERE id = ?', [adminPin, pcId]);

  // Log audit
  await db.run(
    'INSERT INTO audit_logs (id, pc_id, event_type, status, details) VALUES (?, ?, ?, ?, ?)',
    [uuidv4(), pcId, 'ADMIN_PIN_UPDATED', 'SUCCESS', `Admin PIN changed for terminal`]
  );

  res.json({ status: 'SUCCESS', pcId, adminPin, message: 'Admin Emergency PIN updated successfully' });
});

// 11. Toggle Pre-Boot Terminal Lock (Called from Reception Counter Dashboard)
app.post('/api/preboot/toggle', async (req, res) => {
  const { pcId, lockStatus } = req.body;
  const db = await getDb();

  await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [lockStatus, pcId]);
  
  // Log audit event
  await db.run(
    'INSERT INTO audit_logs (id, pc_id, event_type, status, details) VALUES (?, ?, ?, ?, ?)',
    [uuidv4(), pcId, 'PREBOOT_LOCK_TOGGLE', 'SUCCESS', `Counter changed status to ${lockStatus}`]
  );

  relayGateway.notifyMobileStateChange(pcId, lockStatus);
  res.json({ status: 'SUCCESS', pcId, lockStatus });
});

// 12. Fetch Audit Logs
app.get('/api/audit-logs', async (req, res) => {
  const db = await getDb();
  const logs = await db.all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 50');
  res.json({ status: 'SUCCESS', logs });
});

// 13. Cyber Cafe Reception & Pre-Boot Control Dashboard
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
      <title>Cyber Cafe Master Control & Pre-Boot Security Gateway</title>
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
          --text: #f8fafc;
          --text-muted: #94a3b8;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Inter', sans-serif; background: var(--bg); color: var(--text); padding: 24px; }
        header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--card-border); }
        h1 { font-size: 22px; font-weight: 800; color: var(--primary); display: flex; align-items: center; gap: 10px; }
        .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 18px; margin-bottom: 28px; }
        .pc-card { background: var(--card); border: 1px solid var(--card-border); border-radius: 16px; padding: 20px; display: flex; flex-direction: column; gap: 14px; position: relative; overflow: hidden; }
        .pc-card.locked { border-color: rgba(239, 68, 68, 0.4); box-shadow: 0 0 20px rgba(239, 68, 68, 0.08); }
        .pc-card.unlocked { border-color: rgba(16, 185, 129, 0.4); box-shadow: 0 0 20px rgba(16, 185, 129, 0.08); }
        .pc-header { display: flex; justify-content: space-between; align-items: center; }
        .pc-num { font-size: 20px; font-weight: 800; }
        .badge { font-size: 11px; font-weight: 700; padding: 4px 10px; border-radius: 12px; text-transform: uppercase; font-family: 'JetBrains Mono', monospace; }
        .badge-locked { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.4); }
        .badge-unlocked { background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.4); }
        .info-row { display: flex; justify-content: space-between; font-size: 12px; color: var(--text-muted); font-family: 'JetBrains Mono', monospace; }
        .btn-group { display: flex; gap: 8px; margin-top: 4px; }
        .btn { flex: 1; padding: 10px; border-radius: 10px; border: none; font-weight: 700; font-size: 13px; cursor: pointer; transition: all 0.2s; }
        .btn-unlock { background: var(--success); color: #0b0f19; }
        .btn-unlock:hover { filter: brightness(1.1); transform: translateY(-1px); }
        .btn-lock { background: var(--danger); color: #fff; }
        .btn-lock:hover { filter: brightness(1.1); transform: translateY(-1px); }
        .btn-pin { background: rgba(56, 189, 248, 0.15); color: var(--primary); border: 1px solid rgba(56, 189, 248, 0.3); font-size: 11px; padding: 4px 8px; border-radius: 6px; cursor: pointer; }
        .btn-pin:hover { background: rgba(56, 189, 248, 0.3); }
        .section-title { font-size: 16px; font-weight: 700; color: #fff; margin-bottom: 12px; display: flex; align-items: center; gap: 8px; }
        .logs-card { background: var(--card); border: 1px solid var(--card-border); border-radius: 16px; padding: 18px; }
        table { width: 100%; border-collapse: collapse; font-size: 13px; }
        th, td { text-align: left; padding: 10px 12px; border-bottom: 1px solid var(--card-border); }
        th { color: var(--text-muted); font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
        .mono { font-family: 'JetBrains Mono', monospace; }
      </style>
    </head>
    <body>
      <header>
        <div>
          <h1>⚡ Cyber Cafe Pre-Boot & Session Control Hub</h1>
          <p style="color: var(--text-muted); font-size: 13px; margin-top: 4px;">Controls UEFI Pre-Boot OS authorization, live sessions & PC lock states</p>
        </div>
        <div style="font-family: 'JetBrains Mono'; font-size: 13px; color: var(--primary);">
          Port: ${PORT} | Active Terminals: ${pcs.length}
        </div>
      </header>

      <div class="section-title">🖥️ Workstation Terminals (Pre-Boot & Desktop Status)</div>
      <div class="grid">
        ${pcs.map(p => `
          <div class="pc-card ${p.lock_status === 'LOCKED' ? 'locked' : 'unlocked'}" id="card-${p.id}">
            <div class="pc-header">
              <span class="pc-num">${p.pc_number}</span>
              <span class="badge ${p.lock_status === 'LOCKED' ? 'badge-locked' : 'badge-unlocked'}">${p.lock_status}</span>
            </div>
            <div>
              <div style="font-weight: 600; font-size: 14px;">${p.device_name}</div>
              <div class="info-row" style="margin-top: 6px;"><span>MAC:</span><span>${p.mac_address || 'AA:BB:CC:DD:EE:01'}</span></div>
              <div class="info-row"><span>Pre-Boot State:</span><span style="color:${p.lock_status === 'LOCKED' ? '#f87171' : '#34d399'}">${p.lock_status === 'LOCKED' ? 'BOOT BLOCKED' : 'BOOT READY'}</span></div>
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
                ? `<button class="btn btn-unlock" onclick="togglePc('${p.id}', 'UNLOCKED')">🔓 Allow Boot</button>`
                : `<button class="btn btn-lock" onclick="togglePc('${p.id}', 'LOCKED')">🔒 Lock Terminal</button>`
              }
            </div>
          </div>
        `).join('')}
      </div>

      <div class="section-title">📜 Pre-Boot & Security Audit Trail</div>
      <div class="logs-card">
        <table>
          <tr><th>Timestamp</th><th>Terminal</th><th>Event</th><th>Status</th><th>Details</th></tr>
          ${logs.map(l => `
            <tr>
              <td class="mono">${l.created_at}</td>
              <td class="mono"><strong>${l.pc_id || 'SYSTEM'}</strong></td>
              <td class="mono"><code>${l.event_type}</code></td>
              <td><span style="color:${l.status === 'SUCCESS' ? '#34d399' : '#f87171'}">${l.status}</span></td>
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
            if (res.ok) {
              window.location.reload();
            }
          } catch (e) {
            alert('Failed to update terminal status: ' + e.message);
          }
        }

        async function editPin(pcId, pcNumber, currentPin) {
          const newPin = prompt('Enter new 6-digit Emergency Admin PIN for ' + pcNumber + ':', currentPin);
          if (!newPin || newPin.trim() === '' || newPin === currentPin) return;

          try {
            const res = await fetch('/api/devices/pc/set-pin', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ pcId, adminPin: newPin.trim() })
            });
            if (res.ok) {
              alert('Emergency PIN for ' + pcNumber + ' successfully updated to: ' + newPin.trim());
              window.location.reload();
            } else {
              alert('Failed to update PIN');
            }
          } catch (e) {
            alert('Error: ' + e.message);
          }
        }
      </script>
    </body>
    </html>
  `);
});


server.listen(PORT, () => {
  console.log(`=======================================================`);
  console.log(`🔒 Cyber Cafe Relay & Pre-Boot Gateway running on http://localhost:${PORT}`);
  console.log(`⚡ WebSocket Server listening on ws://localhost:${PORT}`);
  console.log(`=======================================================`);
});

