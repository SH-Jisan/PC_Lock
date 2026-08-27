"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.verifyCommandSignature = verifyCommandSignature;
exports.generateDeviceKeyPair = generateDeviceKeyPair;
const crypto_1 = __importDefault(require("crypto"));
// In-memory nonce cache for anti-replay (stores nonces for 5 minutes)
const nonceCache = new Map();
function verifyCommandSignature(payload, publicKeyPemOrDer) {
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
        let keyObject;
        if (publicKeyPemOrDer.includes('BEGIN PUBLIC KEY')) {
            keyObject = crypto_1.default.createPublicKey(publicKeyPemOrDer);
        }
        else {
            // DER formatted Ed25519 key (SPKI prefix for Ed25519: 302a300506032b6570032100 + 32 byte key)
            const rawBuf = Buffer.from(publicKeyPemOrDer, 'hex');
            if (rawBuf.length === 32) {
                const spkiHeader = Buffer.from('302a300506032b6570032100', 'hex');
                keyObject = crypto_1.default.createPublicKey({
                    key: Buffer.concat([spkiHeader, rawBuf]),
                    format: 'der',
                    type: 'spki',
                });
            }
            else {
                keyObject = crypto_1.default.createPublicKey({
                    key: rawBuf,
                    format: 'der',
                    type: 'spki',
                });
            }
        }
        const signatureBuf = Buffer.from(payload.signature, 'hex');
        const isVerified = crypto_1.default.verify(null, // Ed25519 does not use separate digest algorithm parameter
        Buffer.from(canonicalData, 'utf-8'), keyObject, signatureBuf);
        return isVerified
            ? { valid: true }
            : { valid: false, reason: 'Invalid Ed25519 cryptographic signature' };
    }
    catch (err) {
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
function generateDeviceKeyPair() {
    const { publicKey, privateKey } = crypto_1.default.generateKeyPairSync('ed25519');
    const pubRaw = publicKey.export({ type: 'spki', format: 'der' }).subarray(-32);
    const privRaw = privateKey.export({ type: 'pkcs8', format: 'der' }).subarray(-32);
    return {
        publicKeyHex: pubRaw.toString('hex'),
        privateKeyHex: privRaw.toString('hex'),
    };
}
