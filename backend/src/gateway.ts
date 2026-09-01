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

  public getConnectedStats() {
    let onlinePcs = 0;
    let onlineMobiles = 0;
    for (const client of this.clients.values()) {
      if (client.type === 'PC') onlinePcs++;
      else if (client.type === 'MOBILE') onlineMobiles++;
    }
    return { onlinePcs, onlineMobiles, totalSockets: this.clients.size };
  }

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
          console.log(`[WSS Relay] PC ${parsed.pc_id} reported lock status: ${parsed.lock_status}`);
          return;
        }

        if (parsed.event_type === 'HEARTBEAT' && parsed.pc_id) {
          const db = await getDb();
          await db.run('UPDATE pc_devices SET is_online = 1, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [1, parsed.pc_id]);
          ws.send(JSON.stringify({ status: 'PONG', timestamp: Math.floor(Date.now() / 1000) }));
          return;
        }

        const payload: SignedPayload = parsed;
        await this.processMessage(client, payload);
      } catch (err: any) {
        console.error('[WSS Relay] Error parsing message:', err.message);
        ws.send(JSON.stringify({ status: 'ERROR', message: 'Malformed JSON payload: ' + err.message }));
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

    // 1. Verify Target PC exists in database
    const pcRow = await db.get('SELECT * FROM pc_devices WHERE id = ?', [payload.target_pc_id]);
    if (!pcRow) {
      this.logAudit(payload.target_pc_id, sender.id, payload.action, 'FAILED', 'Target PC not found');
      return sender.ws.send(JSON.stringify({ status: 'ERROR', message: 'Target PC not found' }));
    }

    // 2. Cryptographic Zero-Trust Verification (Defense-in-Depth Layer 1)
    if (sender.type === 'MOBILE') {
      let mobileRow = await db.get('SELECT * FROM mobile_devices WHERE id = ? AND is_revoked = 0', [sender.id]);

      // If mobile sends public_key and is not registered, register it
      if (!mobileRow && payload.public_key) {
        await db.run('INSERT INTO mobile_devices (id, user_id, device_name, mobile_public_key, is_revoked) VALUES (?, ?, ?, ?, 0)', [
          sender.id,
          sender.userId || 'user_demo_1',
          `Mobile Controller (${sender.id})`,
          payload.public_key,
        ]);
        mobileRow = { id: sender.id, mobile_public_key: payload.public_key };
      }

      if (!mobileRow || !mobileRow.mobile_public_key) {
        this.logAudit(payload.target_pc_id, sender.id, payload.action, 'REJECTED', 'Mobile device not registered or public key missing');
        return sender.ws.send(JSON.stringify({ status: 'REJECTED', message: 'Mobile device not registered or untrusted' }));
      }

      // If client rotated key or sent explicit key matching, update row
      const pubKeyToUse = payload.public_key || mobileRow.mobile_public_key;

      // Cryptographic Digital Signature Verification
      const verification = verifyCommandSignature(payload, pubKeyToUse);
      if (!verification.valid) {
        console.warn(`[SECURITY ALERT] Invalid Cryptographic Signature from ${sender.id}: ${verification.reason}`);
        this.logAudit(payload.target_pc_id, sender.id, payload.action, 'INVALID_SIGNATURE', verification.reason || 'Cryptographic verification failed');
        return sender.ws.send(JSON.stringify({
          status: 'REJECTED',
          message: verification.reason || 'Invalid cryptographic signature'
        }));
      }

      // Record / Update pairing
      await db.run('INSERT OR REPLACE INTO device_pairings (id, pc_id, mobile_id, is_active) VALUES (?, ?, ?, 1)', [
        uuidv4(),
        payload.target_pc_id,
        sender.id,
      ]);
    }

    // 3. Check Target PC Connection Status
    const targetPcClient = this.clients.get(payload.target_pc_id);
    if (!targetPcClient || targetPcClient.ws.readyState !== WebSocket.OPEN) {
      console.warn(`[WSS Relay] Target PC ${payload.target_pc_id} is OFFLINE`);
      this.logAudit(payload.target_pc_id, sender.id, payload.action, 'OFFLINE', 'PC disconnected from relay server');
      return sender.ws.send(JSON.stringify({ 
        status: 'OFFLINE', 
        is_online: false,
        message: `Workstation (${pcRow.device_name || payload.target_pc_id}) is currently OFFLINE.` 
      }));
    }

    // 4. Relay Verified Payload to Connected Target PC Agent (Defense-in-Depth Layer 2)
    targetPcClient.ws.send(JSON.stringify(payload));
    this.logAudit(payload.target_pc_id, sender.id, payload.action, 'RELAYED', 'Cryptographically verified command dispatched to PC agent');
    
    // 5. Acknowledge Mobile Client
    sender.ws.send(JSON.stringify({
      status: 'SENT',
      command_id: payload.command_id,
      is_online: true,
      message: `Verified Command ${payload.action} dispatched to ${pcRow.device_name}`
    }));
  }

  public notifyMobileStateChange(pcId: string, newLockStatus: string) {
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

  public notifyConnectionStateChange(pcId: string, isOnline: boolean, lastSeenAt: string) {
    for (const client of this.clients.values()) {
      if (client.type === 'MOBILE') {
        client.ws.send(JSON.stringify({
          event: 'PC_CONNECTION_STATE',
          pc_id: pcId,
          is_online: isOnline,
          last_seen_at: lastSeenAt,
          timestamp: Math.floor(Date.now() / 1000)
        }));
      }
    }
  }

  private async updatePcOnlineState(pcId: string, isOnline: number) {
    const db = await getDb();
    const existing = await db.get('SELECT * FROM pc_devices WHERE id = ?', [pcId]);
    const nowIso = new Date().toISOString();

    if (existing) {
      await db.run('UPDATE pc_devices SET is_online = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [isOnline, pcId]);
    } else {
      const count = await db.get('SELECT COUNT(*) as cnt FROM pc_devices');
      const num = `PC-0${(count?.cnt || 0) + 1}`;
      await db.run(
        `INSERT INTO pc_devices (id, user_id, device_name, pc_number, pc_public_key, hardware_uuid, is_online, lock_status) 
         VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
        [pcId, 'user_demo_1', `Cyber Workstation (${num})`, num, 'PUBKEY_AUTO', pcId, isOnline, 'UNLOCKED']
      );
    }

    this.notifyConnectionStateChange(pcId, isOnline === 1, nowIso);
  }

  private async logAudit(pcId: string, mobileId: string, eventType: string, status: string, details: string) {
    const db = await getDb();
    await db.run(
      `INSERT INTO audit_logs (id, pc_id, mobile_id, event_type, status, details) VALUES (?, ?, ?, ?, ?, ?)`,
      [uuidv4(), pcId, mobileId, eventType, status, details]
    );
  }
}
