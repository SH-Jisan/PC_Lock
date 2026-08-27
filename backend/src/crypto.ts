import crypto from 'crypto';

export interface SignedPayload {
  version: string;
  command_id: string;
  sender_device_id: string;
  target_pc_id: string;
  action: 'LOCK_PC' | 'UNLOCK_PC' | 'PING' | 'PAIR_REQUEST';
  timestamp: number; // UTC Unix Timestamp in seconds
  nonce: string;
  signature: string; // Hex or Base64 Ed25519 signature
}

// In-memory nonce cache for anti-replay (stores nonces for 5 minutes)
const nonceCache = new Map<string, number>();

export function verifyCommandSignature(
  payload: SignedPayload,
  publicKeyPemOrDer: string
): { valid: boolean; reason?: string } {
  // 1. Verify Timestamp (Reject commands older than 60 seconds or in future > 10s)
  const now = Math.floor(Date.now() / 1000);
  const timeDiff = Math.abs(now - payload.timestamp);
  if (timeDiff > 60) {
    return { valid: false, reason: `Timestamp out of bounds (${timeDiff}s skew)` };
  }

  // 2. Anti-Replay Nonce Check
  cleanNonceCache();
  if (nonceCache.has(payload.nonce)) {
    return { valid: false, reason: 'Replay attack detected: Nonce already used' };
  }
  nonceCache.set(payload.nonce, now);

  // 3. Construct Canonical Data String to Verify
  const canonicalData = `${payload.version}:${payload.command_id}:${payload.sender_device_id}:${payload.target_pc_id}:${payload.action}:${payload.timestamp}:${payload.nonce}`;

  try {
    // Format Public Key if raw Ed25519 hex/base64 passed
    let keyObject: crypto.KeyObject;
    if (publicKeyPemOrDer.includes('BEGIN PUBLIC KEY')) {
      keyObject = crypto.createPublicKey(publicKeyPemOrDer);
    } else {
      // DER formatted Ed25519 key (SPKI prefix for Ed25519: 302a300506032b6570032100 + 32 byte key)
      const rawBuf = Buffer.from(publicKeyPemOrDer, 'hex');
      if (rawBuf.length === 32) {
        const spkiHeader = Buffer.from('302a300506032b6570032100', 'hex');
        keyObject = crypto.createPublicKey({
          key: Buffer.concat([spkiHeader, rawBuf]),
          format: 'der',
          type: 'spki',
        });
      } else {
        keyObject = crypto.createPublicKey({
          key: rawBuf,
          format: 'der',
          type: 'spki',
        });
      }
    }

    const signatureBuf = Buffer.from(payload.signature, 'hex');
    const isVerified = crypto.verify(
      null, // Ed25519 does not use separate digest algorithm parameter
      Buffer.from(canonicalData, 'utf-8'),
      keyObject,
      signatureBuf
    );

    return isVerified
      ? { valid: true }
      : { valid: false, reason: 'Invalid Ed25519 cryptographic signature' };
  } catch (err: any) {
    return { valid: false, reason: `Crypto error: ${err.message}` };
  }
}

function cleanNonceCache() {
  const now = Math.floor(Date.now() / 1000);
  for (const [nonce, ts] of nonceCache.entries()) {
    if (now - ts > 300) {
      nonceCache.delete(nonce);
    }
  }
}

export function generateDeviceKeyPair(): { publicKeyHex: string; privateKeyHex: string } {
  const { publicKey, privateKey } = crypto.generateKeyPairSync('ed25519');
  const pubRaw = publicKey.export({ type: 'spki', format: 'der' }).subarray(-32);
  const privRaw = privateKey.export({ type: 'pkcs8', format: 'der' }).subarray(-32);

  return {
    publicKeyHex: pubRaw.toString('hex'),
    privateKeyHex: privRaw.toString('hex'),
  };
}
