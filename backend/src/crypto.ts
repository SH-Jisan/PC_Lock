import crypto from 'crypto';

export interface SignedPayload {
  version: string;
  command_id: string;
  sender_device_id: string;
  target_pc_id: string;
  action: 'LOCK_PC' | 'UNLOCK_PC' | 'PING' | 'PAIR_REQUEST';
  timestamp: number; // UTC Unix Timestamp in seconds
  nonce: string;
  signature: string; // Hex signature
  public_key?: string; // Optional exported public key in SPKI hex
}

// In-memory nonce cache for anti-replay (stores nonces for 5 minutes)
const nonceCache = new Map<string, number>();

export function verifyCommandSignature(
  payload: SignedPayload,
  publicKeyHexOrPem: string
): { valid: boolean; reason?: string } {
  // 1. Verify Timestamp (Reject commands older than 60 seconds or skewed in future > 10s)
  const now = Math.floor(Date.now() / 1000);
  const timeDiff = Math.abs(now - payload.timestamp);
  if (timeDiff > 60) {
    return { valid: false, reason: `Timestamp skew error: ${timeDiff}s skew exceeds 60s limit` };
  }

  // 2. Anti-Replay Nonce Check
  cleanNonceCache();
  if (nonceCache.has(payload.nonce)) {
    return { valid: false, reason: 'Replay attack detected: Nonce already used' };
  }
  nonceCache.set(payload.nonce, now);

  // 3. Construct Canonical Data String to Verify
  const canonicalData = `${payload.version}:${payload.command_id}:${payload.sender_device_id}:${payload.target_pc_id}:${payload.action}:${payload.timestamp}:${payload.nonce}`;

  if (!payload.signature || payload.signature.trim() === '') {
    return { valid: false, reason: 'Missing cryptographic signature' };
  }

  try {
    const rawKeyBuf = Buffer.from(publicKeyHexOrPem, 'hex');
    const signatureBuf = Buffer.from(payload.signature, 'hex');
    let keyObject: crypto.KeyObject;

    if (publicKeyHexOrPem.includes('BEGIN PUBLIC KEY')) {
      keyObject = crypto.createPublicKey(publicKeyHexOrPem);
    } else if (rawKeyBuf.length === 32) {
      // Raw 32-byte Ed25519 key -> Add SPKI Header
      const spkiHeader = Buffer.from('302a300506032b6570032100', 'hex');
      keyObject = crypto.createPublicKey({
        key: Buffer.concat([spkiHeader, rawKeyBuf]),
        format: 'der',
        type: 'spki',
      });
    } else {
      // Standard SPKI DER formatted ECDSA / Ed25519 key
      keyObject = crypto.createPublicKey({
        key: rawKeyBuf,
        format: 'der',
        type: 'spki',
      });
    }

    // Try verifying with SHA256 (for ECDSA NIST P-256) first, then fallback to null (for Ed25519)
    let isVerified = false;
    try {
      isVerified = crypto.verify(
        'SHA256',
        Buffer.from(canonicalData, 'utf-8'),
        {
          key: keyObject,
          dsaEncoding: 'ieee-p1363',
        },
        signatureBuf
      );
    } catch {
      try {
        isVerified = crypto.verify('SHA256', Buffer.from(canonicalData, 'utf-8'), keyObject, signatureBuf);
      } catch {
        try {
          isVerified = crypto.verify(null, Buffer.from(canonicalData, 'utf-8'), keyObject, signatureBuf);
        } catch {}
      }
    }

    return isVerified
      ? { valid: true }
      : { valid: false, reason: 'Cryptographic signature verification failed (Key/Data mismatch)' };
  } catch (err: any) {
    return { valid: false, reason: `Cryptographic processing error: ${err.message}` };
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
