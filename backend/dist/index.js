"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const express_1 = __importDefault(require("express"));
const http_1 = __importDefault(require("http"));
const ws_1 = __importDefault(require("ws"));
const cors_1 = __importDefault(require("cors"));
const uuid_1 = require("uuid");
const jsonwebtoken_1 = __importDefault(require("jsonwebtoken"));
const db_1 = require("./db");
const gateway_1 = require("./gateway");
const JWT_SECRET = process.env.JWT_SECRET || 'pc_security_master_jwt_secret_2026';
const PORT = process.env.PORT || 4000;
const app = (0, express_1.default)();
app.use((0, cors_1.default)());
app.use(express_1.default.json());
const server = http_1.default.createServer(app);
const wss = new ws_1.default.Server({ server });
const relayGateway = new gateway_1.RelayGateway();
// WebSocket Connection Router
wss.on('connection', (ws, req) => {
    const urlParams = new URLSearchParams(req.url?.split('?')[1] || '');
    const deviceId = urlParams.get('device_id');
    const deviceType = urlParams.get('device_type');
    const token = urlParams.get('token');
    if (!deviceId || !deviceType) {
        ws.close(4001, 'Missing device_id or device_type');
        return;
    }
    // Token validation (optional bypass for demo PC agent)
    let userId = 'user_demo_1';
    if (token) {
        try {
            const decoded = jsonwebtoken_1.default.verify(token, JWT_SECRET);
            userId = decoded.userId;
        }
        catch {
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
    const db = await (0, db_1.getDb)();
    let user = await db.get('SELECT * FROM users WHERE email = ?', [email]);
    if (!user) {
        // Auto-create user for frictionless onboarding demo
        const userId = (0, uuid_1.v4)();
        await db.run('INSERT INTO users (id, email, password_hash) VALUES (?, ?, ?)', [userId, email, 'hash_placeholder']);
        user = { id: userId, email };
    }
    const token = jsonwebtoken_1.default.sign({ userId: user.id, email: user.email }, JWT_SECRET, { expiresIn: '7d' });
    res.json({ status: 'SUCCESS', token, userId: user.id });
});
// 3. Register PC Device
app.post('/api/devices/pc/register', async (req, res) => {
    const { userId, deviceName, pcPublicKey, hardwareUuid } = req.body;
    const db = await (0, db_1.getDb)();
    const existing = await db.get('SELECT * FROM pc_devices WHERE hardware_uuid = ?', [hardwareUuid]);
    let pcId = existing?.id || (0, uuid_1.v4)();
    if (existing) {
        await db.run('UPDATE pc_devices SET device_name = ?, pc_public_key = ?, is_online = 1 WHERE id = ?', [
            deviceName,
            pcPublicKey,
            pcId,
        ]);
    }
    else {
        await db.run('INSERT INTO pc_devices (id, user_id, device_name, pc_public_key, hardware_uuid, is_online) VALUES (?, ?, ?, ?, ?, 1)', [pcId, userId || 'user_demo_1', deviceName, pcPublicKey, hardwareUuid]);
    }
    res.json({ status: 'SUCCESS', pcId, message: 'PC Identity Registered' });
});
// 4. Register Mobile Device
app.post('/api/devices/mobile/register', async (req, res) => {
    const { userId, deviceName, mobilePublicKey, deviceToken } = req.body;
    const db = await (0, db_1.getDb)();
    const mobileId = (0, uuid_1.v4)();
    await db.run('INSERT INTO mobile_devices (id, user_id, device_name, mobile_public_key, device_token) VALUES (?, ?, ?, ?, ?)', [mobileId, userId || 'user_demo_1', deviceName, mobilePublicKey, deviceToken || '']);
    res.json({ status: 'SUCCESS', mobileId, message: 'Mobile Device Registered' });
});
// 5. Initiate Device Pairing (QR Payload)
app.post('/api/pairing/initiate', async (req, res) => {
    const { pcId } = req.body;
    const db = await (0, db_1.getDb)();
    const pc = await db.get('SELECT * FROM pc_devices WHERE id = ?', [pcId]);
    if (!pc)
        return res.status(404).json({ error: 'PC not found' });
    const pairingSecret = (0, uuid_1.v4)();
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
    const db = await (0, db_1.getDb)();
    const pairingId = (0, uuid_1.v4)();
    await db.run('INSERT OR REPLACE INTO device_pairings (id, pc_id, mobile_id, is_active) VALUES (?, ?, ?, 1)', [pairingId, pcId, mobileId]);
    res.json({ status: 'SUCCESS', pairingId, message: 'Pairing Established!' });
});
// 7. Get Device Status
app.get('/api/devices/status', async (req, res) => {
    const db = await (0, db_1.getDb)();
    const pcs = await db.all('SELECT * FROM pc_devices');
    const mobiles = await db.all('SELECT * FROM mobile_devices WHERE is_revoked = 0');
    const pairings = await db.all('SELECT * FROM device_pairings WHERE is_active = 1');
    res.json({ status: 'SUCCESS', pcs, mobiles, pairings });
});
// 8. Update PC Lock Status (Called by PC Agent when state changes)
app.post('/api/devices/pc/status-update', async (req, res) => {
    const { pcId, lockStatus } = req.body;
    const db = await (0, db_1.getDb)();
    await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [lockStatus, pcId]);
    relayGateway.notifyMobileStateChange(pcId, lockStatus);
    res.json({ status: 'SUCCESS', lockStatus });
});
// 9. Fetch Audit Logs
app.get('/api/audit-logs', async (req, res) => {
    const db = await (0, db_1.getDb)();
    const logs = await db.all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 50');
    res.json({ status: 'SUCCESS', logs });
});
// 10. Admin Web Dashboard
app.get('/', async (req, res) => {
    const db = await (0, db_1.getDb)();
    const pcs = await db.all('SELECT * FROM pc_devices');
    const logs = await db.all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 10');
    res.send(`
    <!DOCTYPE html>
    <html>
    <head>
      <title>PC Security Relay - Management Dashboard</title>
      <style>
        body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #0f172a; color: #f8fafc; margin: 0; padding: 24px; }
        h1 { color: #38bdf8; margin-bottom: 8px; }
        .card { background: #1e293b; border-radius: 12px; padding: 20px; margin-bottom: 20px; border: 1px solid #334155; }
        table { width: 100%; border-collapse: collapse; margin-top: 12px; }
        th, td { text-align: left; padding: 12px; border-bottom: 1px solid #334155; }
        th { color: #94a3b8; font-size: 12px; text-transform: uppercase; }
        .badge { padding: 4px 8px; border-radius: 6px; font-weight: bold; font-size: 12px; }
        .online { background: #065f46; color: #34d399; }
        .offline { background: #7f1d1d; color: #fca5a5; }
        .locked { background: #991b1b; color: #f87171; }
        .unlocked { background: #1e3a8a; color: #60a5fa; }
      </style>
    </head>
    <body>
      <h1>🔒 PC Security System Relay Gateway</h1>
      <p style="color:#94a3b8">Active E2EE Relay Server & Device Management Node</p>

      <div class="card">
        <h2>Registered PCs (${pcs.length})</h2>
        <table>
          <tr><th>PC Name</th><th>Hardware UUID</th><th>Online State</th><th>Lock State</th><th>Last Seen</th></tr>
          ${pcs
        .map((p) => `
            <tr>
              <td><strong>${p.device_name}</strong></td>
              <td><code>${p.hardware_uuid}</code></td>
              <td><span class="badge ${p.is_online ? 'online' : 'offline'}">${p.is_online ? 'ONLINE' : 'OFFLINE'}</span></td>
              <td><span class="badge ${p.lock_status === 'LOCKED' ? 'locked' : 'unlocked'}">${p.lock_status}</span></td>
              <td>${p.last_seen_at || 'N/A'}</td>
            </tr>
          `)
        .join('')}
        </table>
      </div>

      <div class="card">
        <h2>Recent Security Audit Trail</h2>
        <table>
          <tr><th>Timestamp</th><th>Event</th><th>Status</th><th>Details</th></tr>
          ${logs
        .map((l) => `
            <tr>
              <td>${l.created_at}</td>
              <td><code>${l.event_type}</code></td>
              <td>${l.status}</td>
              <td>${l.details}</td>
            </tr>
          `)
        .join('')}
        </table>
      </div>
    </body>
    </html>
  `);
});
server.listen(PORT, () => {
    console.log(`=======================================================`);
    console.log(`🔒 PC Security Relay Gateway running on http://localhost:${PORT}`);
    console.log(`⚡ WebSocket Server listening on ws://localhost:${PORT}`);
    console.log(`=======================================================`);
});
