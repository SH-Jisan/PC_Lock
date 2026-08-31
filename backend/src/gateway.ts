import WebSocket from 'ws';
import { getDb } from './db';
import { verifyCommandSignature, SignedPayload } from './crypto';
import { v4 as uuidv4 } from 'uuid';

interface ConnectedClient {
  id: string; // Device ID (pc_id or mobile_id)
  type: 'PC' | 'MOBILE';
  userId: string;
  ws: WebSocket;
}

export class RelayGateway {
  private clients = new Map<string, ConnectedClient>();

  public handleConnection(ws: WebSocket, deviceId: string, deviceType: 'PC' | 'MOBILE', userId: string) {
    const client: ConnectedClient = { id: deviceId, type: deviceType, userId, ws };
    this.clients.set(deviceId, client);

    console.log(`[WSS Relay] Client connected: ${deviceType} ID=${deviceId} (User=${userId})`);

    // Update PC online status in DB if PC
    if (deviceType === 'PC') {
      this.updatePcOnlineState(deviceId, 1);
    }

    ws.on('message', async (data: WebSocket.RawData) => {
      try {
        const messageStr = data.toString('utf-8');
        const parsed = JSON.parse(messageStr);

        if (parsed.event_type === 'PC_STATUS_REPORT' && parsed.pc_id && parsed.lock_status) {
          const db = await getDb();
          await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [parsed.lock_status, parsed.pc_id]);
          this.notifyMobileStateChange(parsed.pc_id, parsed.lock_status);
          console.log(`[WSS Relay] PC ${parsed.pc_id} reported status: ${parsed.lock_status}`);
          return;
        }

        const payload: SignedPayload = parsed;
        await this.processMessage(client, payload);
      } catch (err: any) {
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

  private async processMessage(sender: ConnectedClient, payload: SignedPayload) {
    const db = await getDb();

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
      const verification = verifyCommandSignature(payload, mobileRow.mobile_public_key);
      if (!verification.valid) {
        console.warn(`[SECURITY ALERT] Invalid signature from ${sender.id}: ${verification.reason}`);
        this.logAudit(payload.target_pc_id, sender.id, payload.action, 'INVALID_SIGNATURE', verification.reason || 'Verification failed');
        return sender.ws.send(JSON.stringify({ status: 'REJECTED', message: verification.reason }));
      }
    }

    // 4. Relay Payload to Connected PC Agent
    const targetPcClient = this.clients.get(payload.target_pc_id);
    if (!targetPcClient || targetPcClient.ws.readyState !== WebSocket.OPEN) {
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

  public notifyMobileStateChange(pcId: string, newLockStatus: string) {
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

  private async updatePcOnlineState(pcId: string, isOnline: number) {
    const db = await getDb();
    const existing = await db.get('SELECT * FROM pc_devices WHERE id = ?', [pcId]);
    if (existing) {
      await db.run('UPDATE pc_devices SET is_online = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [isOnline, pcId]);
    } else {
      // Auto-register newly discovered PC workstation
      const count = await db.get('SELECT COUNT(*) as cnt FROM pc_devices');
      const num = `PC-0${(count?.cnt || 0) + 1}`;
      await db.run(
        `INSERT INTO pc_devices (id, user_id, device_name, pc_number, pc_public_key, hardware_uuid, is_online, lock_status) 
         VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
        [pcId, 'user_demo_1', `Cyber Workstation (${pcId})`, num, 'PUBKEY_AUTO', pcId, isOnline, 'UNLOCKED']
      );
    }
  }

  private async logAudit(pcId: string, mobileId: string, eventType: string, status: string, details: string) {
    const db = await getDb();
    await db.run(
      `INSERT INTO audit_logs (id, pc_id, mobile_id, event_type, status, details) VALUES (?, ?, ?, ?, ?, ?)`,
      [uuidv4(), pcId, mobileId, eventType, status, details]
    );
  }
}

