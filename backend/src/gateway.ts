import WebSocket from 'ws';
import { v4 as uuidv4 } from 'uuid';
import { getDb } from './db';
import { verifyCommandSignature } from './crypto';

interface ConnectedClient {
  id: string;
  type: 'PC' | 'MOBILE';
  userId: string;
  ws: WebSocket;
}

interface SignedPayload {
  version: string;
  command_id: string;
  sender_device_id: string;
  target_pc_id: string;
  action: 'LOCK_PC' | 'UNLOCK_PC';
  timestamp: number;
  nonce: string;
  signature: string;
  public_key?: string;
}

export class RelayGateway {
  private clients: Map<string, ConnectedClient> = new Map();

  public handleConnection(ws: WebSocket, deviceId: string, deviceType: 'PC' | 'MOBILE', userId: string = 'user_demo_1') {
    const client: ConnectedClient = { id: deviceId, type: deviceType, userId, ws };
    this.clients.set(deviceId, client);

    console.log(`[WSS Relay] ${deviceType} connected: ${deviceId} (User: ${userId})`);

    // If PC connected, update its online state in DB and notify mobile clients
    if (deviceType === 'PC') {
      this.updatePcOnlineState(deviceId, 1);
    }

    ws.on('message', async (data: string) => {
      try {
        const payload = JSON.parse(data.toString());

        // Handle Heartbeat / Telemetry Report from PC
        if (payload.event_type === 'HEARTBEAT' || payload.event_type === 'PC_STATUS_REPORT') {
          if (payload.lock_status) {
            const db = await getDb();
            await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [
              payload.lock_status,
              deviceId
            ]);
            this.notifyMobileStateChange(deviceId, payload.lock_status);
          }
          ws.send(JSON.stringify({ status: 'PONG', timestamp: Math.floor(Date.now() / 1000) }));
          return;
        }

        // Process Command Dispatch
        await this.processMessage(client, payload as SignedPayload);
      } catch (err: any) {
        console.error(`[WSS Relay Error] Failed to process message from ${deviceId}:`, err.message);
        ws.send(JSON.stringify({ status: 'ERROR', message: 'Invalid payload structure' }));
      }
    });

    ws.on('close', () => {
      console.log(`[WSS Relay] ${deviceType} disconnected: ${deviceId}`);
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

    // 1. Verify Target PC exists in database (check by id or pc_number)
    const pcRow = await db.get('SELECT * FROM pc_devices WHERE id = ? OR pc_number = ?', [
      payload.target_pc_id,
      payload.target_pc_id
    ]);

    if (!pcRow) {
      this.logAudit(payload.target_pc_id, sender.id, payload.action, 'FAILED', 'Target PC not found');
      return sender.ws.send(JSON.stringify({ status: 'ERROR', message: 'Target PC not found' }));
    }

    const actualPcId = pcRow.id;

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

      if (payload.signature && (payload.public_key || mobileRow?.mobile_public_key)) {
        const pubKeyToUse = payload.public_key || mobileRow.mobile_public_key;
        const verification = verifyCommandSignature(payload, pubKeyToUse);
        if (!verification.valid) {
          console.warn(`[SECURITY ALERT] Invalid Cryptographic Signature from ${sender.id}: ${verification.reason}`);
          this.logAudit(actualPcId, sender.id, payload.action, 'INVALID_SIGNATURE', verification.reason || 'Cryptographic verification failed');
          return sender.ws.send(JSON.stringify({
            status: 'REJECTED',
            message: verification.reason || 'Invalid cryptographic signature'
          }));
        }
      }

      // Record / Update pairing
      await db.run('INSERT OR REPLACE INTO device_pairings (id, pc_id, mobile_id, is_active) VALUES (?, ?, ?, 1)', [
        uuidv4(),
        actualPcId,
        sender.id,
      ]);
    }

    // 3. Check Target PC Connection Status
    const targetPcClient = this.clients.get(actualPcId);
    if (!targetPcClient || targetPcClient.ws.readyState !== WebSocket.OPEN) {
      console.warn(`[WSS Relay] Target PC ${actualPcId} is OFFLINE`);
      this.logAudit(actualPcId, sender.id, payload.action, 'OFFLINE', 'PC disconnected from relay server');
      return sender.ws.send(JSON.stringify({ 
        status: 'OFFLINE', 
        is_online: false,
        message: `Workstation (${pcRow.device_name || actualPcId}) is currently OFFLINE.` 
      }));
    }

    // 4. Relay Payload to Connected Target PC Agent (Defense-in-Depth Layer 2)
    payload.target_pc_id = actualPcId;
    targetPcClient.ws.send(JSON.stringify(payload));
    this.logAudit(actualPcId, sender.id, payload.action, 'RELAYED', 'Command dispatched to PC agent');
    
    // Update local database lock state
    const newLockStatus = payload.action === 'LOCK_PC' ? 'LOCKED' : 'UNLOCKED';
    await db.run('UPDATE pc_devices SET lock_status = ?, last_seen_at = CURRENT_TIMESTAMP WHERE id = ?', [newLockStatus, actualPcId]);
    this.notifyMobileStateChange(actualPcId, newLockStatus);

    // 5. Acknowledge Mobile Client
    sender.ws.send(JSON.stringify({
      status: 'SENT',
      command_id: payload.command_id,
      is_online: true,
      message: `Command ${payload.action} dispatched to ${pcRow.device_name}`
    }));
  }

  public async dispatchDirectCommand(targetPcIdentifier: string, action: 'LOCK_PC' | 'UNLOCK_PC'): Promise<boolean> {
    const db = await getDb();
    const pcRow = await db.get('SELECT * FROM pc_devices WHERE id = ? OR pc_number = ?', [
      targetPcIdentifier,
      targetPcIdentifier
    ]);

    const actualPcId = pcRow?.id || targetPcIdentifier;
    const targetPcClient = this.clients.get(actualPcId);

    if (targetPcClient && targetPcClient.ws.readyState === WebSocket.OPEN) {
      const payload = {
        version: '1.0',
        command_id: uuidv4(),
        sender_device_id: 'WEB_ADMIN',
        target_pc_id: actualPcId,
        action: action,
        timestamp: Math.floor(Date.now() / 1000),
        nonce: uuidv4(),
        signature: ''
      };
      targetPcClient.ws.send(JSON.stringify(payload));
      this.logAudit(actualPcId, 'WEB_ADMIN', action, 'DISPATCHED', 'Direct command dispatched from Admin Console');
      return true;
    }
    return false;
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

  private async logAudit(pcId: string, senderId: string, action: string, status: string, details: string) {
    try {
      const db = await getDb();
      await db.run(
        'INSERT INTO audit_logs (id, pc_id, triggered_by, action, status, details) VALUES (?, ?, ?, ?, ?, ?)',
        [uuidv4(), pcId, senderId, action, status, details]
      );
    } catch (err: any) {
      console.error('[WSS Audit Log Error]:', err.message);
    }
  }

  public getConnectedStats() {
    let onlinePcs = 0;
    let onlineMobiles = 0;

    for (const client of this.clients.values()) {
      if (client.ws.readyState === WebSocket.OPEN) {
        if (client.type === 'PC') onlinePcs++;
        if (client.type === 'MOBILE') onlineMobiles++;
      }
    }

    return {
      onlinePcs,
      onlineMobiles,
      totalSockets: this.clients.size
    };
  }
}
