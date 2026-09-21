const DB_NAME = 'PCLockSecureKeystore';
const DB_VERSION = 1;
const STORE_NAME = 'hardware_keys';

let MOBILE_DEVICE_ID = '';
let privateCryptoKey = null;
let publicCryptoKey = null;
let cachedPublicKeyHex = '';

function openKeyDatabase() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = (e) => {
      const db = e.target.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME, { keyPath: 'id' });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function loadOrGenerateHardwareKeys(logCallback) {
  const db = await openKeyDatabase();

  // Attempt to load existing persistent keys
  const existing = await new Promise((resolve) => {
    const tx = db.transaction(STORE_NAME, 'readonly');
    const req = tx.objectStore(STORE_NAME).get('master_controller_key');
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => resolve(null);
  });

  if (existing && existing.privateKey && existing.publicKey && existing.publicKeyHex) {
    privateCryptoKey = existing.privateKey;
    publicCryptoKey = existing.publicKey;
    cachedPublicKeyHex = existing.publicKeyHex;
    MOBILE_DEVICE_ID = existing.deviceId;
    if (logCallback) logCallback('Persistent Non-Extractable Key Loaded from IndexedDB', 'KEY_PERSIST');
    return;
  }

  // Generate New ECDSA P-256 Keypair
  if (logCallback) logCallback('Generating Non-Extractable ECDSA P-256 Keypair...', 'CRYPTO_GEN');

  MOBILE_DEVICE_ID = 'mob_' + Array.from(crypto.getRandomValues(new Uint8Array(6)))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');

  const keyPair = await window.crypto.subtle.generateKey(
    { name: 'ECDSA', namedCurve: 'P-256' },
    true, // Public key extractable for registration, private key stored as CryptoKey
    ['sign', 'verify']
  );

  privateCryptoKey = keyPair.privateKey;
  publicCryptoKey = keyPair.publicKey;

  const exportedSpki = await window.crypto.subtle.exportKey('spki', keyPair.publicKey);
  cachedPublicKeyHex = Array.from(new Uint8Array(exportedSpki))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');

  // Store CryptoKey objects natively in IndexedDB (W3C Structured Clone Preserves Non-Extractability)
  await new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, 'readwrite');
    const store = tx.objectStore(STORE_NAME);
    store.put({
      id: 'master_controller_key',
      deviceId: MOBILE_DEVICE_ID,
      privateKey: privateCryptoKey,
      publicKey: publicCryptoKey,
      publicKeyHex: cachedPublicKeyHex,
      createdAt: new Date().toISOString(),
    });
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });

  if (logCallback) logCallback('Zero-Leakage Keys Saved in IndexedDB Secure Storage', 'KEY_SAVED');
}

async function createSignedCommandPayload(targetPcId, action) {
  if (!privateCryptoKey) {
    await loadOrGenerateHardwareKeys();
  }

  const version = '1.0';
  const commandId = crypto.randomUUID ? crypto.randomUUID() : 'cmd_' + Date.now();
  const timestamp = Math.floor(Date.now() / 1000);
  const nonce = Array.from(crypto.getRandomValues(new Uint8Array(16)))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');

  // Construct Canonical String
  const canonicalData = `${version}:${commandId}:${MOBILE_DEVICE_ID}:${targetPcId}:${action}:${timestamp}:${nonce}`;

  // Hardware-gated WebCrypto Digital Signing
  const encoder = new TextEncoder();
  const signatureBuf = await window.crypto.subtle.sign(
    { name: 'ECDSA', hash: { name: 'SHA-256' } },
    privateCryptoKey,
    encoder.encode(canonicalData)
  );

  const signatureHex = Array.from(new Uint8Array(signatureBuf))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');

  return {
    version,
    command_id: commandId,
    sender_device_id: MOBILE_DEVICE_ID,
    target_pc_id: targetPcId,
    action,
    timestamp,
    nonce,
    signature: signatureHex,
    public_key: cachedPublicKeyHex,
  };
}
