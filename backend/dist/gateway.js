"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.RelayGateway = void 0;
const ws_1 = __importDefault(require("ws"));
const db_1 = require("./db");
const crypto_1 = require("./crypto");
const uuid_1 = require("uuid");
class RelayGateway {
    clients = new Map();
    handleConnection(ws, deviceId, deviceType, userId) {
        const client = { id: deviceId, type: deviceType, userId, ws };
        this.clients.set(deviceId, client);
        console.log(`[WSS Relay] Client connected: ${deviceType} ID=${deviceId} (User=${userId})`);
        // Update PC online status in DB if PC
        if (deviceType === 'PC') {
            this.updatePcOnlineState(deviceId, 1);
        }
        ws.on('message', async (data) => {
            try {
                const messageStr = data.toString('utf-8');
                const payload = JSON.parse(messageStr);
                await this.processMessage(client, payload);
            }
            catch (err) {
                console.error('[WSS Relay] Error parsing message:', err.message);
                ws.send(JSON.stringify({ status: 'ERROR', message: 'Malformed JSON payload' }));
            }
        });
        ws.on('close', () => {
            console.log(`[WSS Relay] Client disconnected: ${deviceType} ID=${deviceId}`);
            this.clients.delete(deviceId);
            if (deviceType === 'PC') {
                this.updatePcOnlineState(deviceId, 0);
            }
        });
        ws.on('error', (error) => {
            console.error(`[WSS Relay] Client socket error (${deviceId}):`, error);
        });
    }
    async processMessage(sender, payload) {
        const db = await (0, db_1.getDb)();
        console.log(`[WSS Relay] Action: ${payload.action} from ${sender.type} (${sender.id}) -> Target PC (${payload.target_pc_id})`);
        // 1. Verify Target PC Registration
        const pcRow = await db.get('SELECT * FROM pc_devices WHERE id = ?', [payload.target_pc_id]);
        if (!pcRow) {
            this.logAudit(payload.target_pc_id, sender.id, payload.action, 'FAILED', 'Target PC not found');
            return sender.ws.send(JSON.stringify({ status: 'ERROR', message: 'Target PC not found' }));
        }
        // 2. Fetch Sender Mobile Public Key if sent by Mobile
        if (sender.type === 'MOBILE') {
            const mobileRow = await db.get('SELECT * FROM mobile_devices WHERE id = ? AND is_revoked = 0', [sender.id]);
            if (!mobileRow) {
                this.logAudit(payload.target_pc_id, sender.id, payload.action, 'FAILED', 'Mobile device revoked or not registered');
                return sender.ws.send(JSON.stringify({ status: 'ERROR', message: 'Mobile device revoked or untrusted' }));
            }
            // Verify Pairing active
            const pairing = await db.get('SELECT * FROM device_pairings WHERE pc_id = ? AND mobile_id = ? AND is_active = 1', [
                payload.target_pc_id,
                sender.id,
            ]);
            if (!pairing) {
                this.logAudit(payload.target_pc_id, sender.id, payload.action, 'FAILED', 'Device pairing inactive');
                return sender.ws.send(JSON.stringify({ status: 'ERROR', message: 'No active pairing between phone and PC' }));
            }
            // 3. Cryptographic Signature Verification
            const verification = (0, crypto_1.verifyCommandSignature)(payload, mobileRow.mobile_public_key);
            if (!verification.valid) {
                console.warn(`[SECURITY ALERT] Invalid signature from ${sender.id}: ${verification.reason}`);
                this.logAudit(payload.target_pc_id, sender.id, payload.action, 'INVALID_SIGNATURE', verification.reason || 'Verification failed');
                return sender.ws.send(JSON.stringify({ status: 'REJECTED', message: verification.reason }));
            }
        }
        // 4. Relay Payload to Connected PC Agent
        const targetPcClient = this.clients.get(payload.target_pc_id);
        if (!targetPcClient || targetPcClient.ws.readyState !== ws_1.default.OPEN) {
            console.warn(`[WSS Relay] Target PC ${payload.target_pc_id} is offline or unreachable`);
            this.logAudit(payload.target_pc_id, sender.id, payload.action, 'OFFLINE', 'PC disconnected from relay server');
            return sender.ws.send(JSON.stringify({ status: 'QUEUED_OFFLINE', message: 'PC is currently offline. Action stored.' }));
        }
        // Forward signed command directly to PC
        targetPcClient.ws.send(JSON.stringify(payload));
        this.logAudit(payload.target_pc_id, sender.id, payload.action, 'RELAYED', 'Command successfully routed to PC agent');
        // Acknowledge Mobile client
        sender.ws.send(JSON.stringify({
            status: 'SENT',
            command_id: payload.command_id,
            message: `Command ${payload.action} dispatched to PC ${pcRow.device_name}`
        }));
    }
    notifyMobileStateChange(pcId, newLockStatus) {
        // Notify all paired mobile clients connected to WebSocket
        for (const client of this.clients.values()) {
            if (client.type === 'MOBILE') {
                client.ws.send(JSON.stringify({
                    event: 'PC_STATUS_UPDATED',
                    pc_id: pcId,
                    lock_status: newLockStatus,
                    timestamp: Math.floor(Date.now() / 1000)
                }));
            }
        }
    }
    async updatePcOnlineState(pcId, isOnline) {
        const db = await (0, db_1.getDb)();
        await db.run('UPDATE pc_devices SET is_online = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [isOnline, pcId]);
    }
    async logAudit(pcId, mobileId, eventType, status, details) {
        const db = await (0, db_1.getDb)();
        await db.run(`INSERT INTO audit_logs (id, pc_id, mobile_id, event_type, status, details) VALUES (?, ?, ?, ?, ?, ?)`, [(0, uuid_1.v4)(), pcId, mobileId, eventType, status, details]);
    }
}
exports.RelayGateway = RelayGateway;
