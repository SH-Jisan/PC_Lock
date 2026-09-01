const crypto = require('crypto');
const WebSocket = require('ws');

// Generate ECDSA P-256 Keypair
const { privateKey, publicKey } = crypto.generateKeyPairSync('ec', {
  namedCurve: 'prime256v1',
  publicKeyEncoding: { type: 'spki', format: 'der' },
  privateKeyEncoding: { type: 'pkcs8', format: 'der' }
});

const pubKeyHex = publicKey.toString('hex');
const mobileId = 'mob_controller_live';
const targetPcId = 'pc_7de35946';
const action = 'LOCK_PC';
const version = '1.0';
const commandId = 'cmd_' + Date.now();
const timestamp = Math.floor(Date.now() / 1000);
const nonce = crypto.randomBytes(16).toString('hex');

const canonicalData = version + ':' + commandId + ':' + mobileId + ':' + targetPcId + ':' + action + ':' + timestamp + ':' + nonce;
const sign = crypto.createSign('SHA256');
sign.update(canonicalData);
sign.end();
const signatureHex = sign.sign({ key: crypto.createPrivateKey({ key: privateKey, format: 'der', type: 'pkcs8' }), dsaEncoding: 'ieee-p1363' }).toString('hex');

const payload = {
  version,
  command_id: commandId,
  sender_device_id: mobileId,
  target_pc_id: targetPcId,
  action,
  timestamp,
  nonce,
  signature: signatureHex,
  public_key: pubKeyHex
};

console.log('Connecting to wss://pc-lock.onrender.com ...');
const ws = new WebSocket('wss://pc-lock.onrender.com/?device_id=' + mobileId + '&device_type=MOBILE');
ws.on('open', () => {
  console.log('Mobile WS connected. Sending signed LOCK_PC payload:', payload);
  ws.send(JSON.stringify(payload));
});

ws.on('message', (msg) => {
  console.log('Server response to Mobile:', msg.toString());
  setTimeout(() => process.exit(0), 2000);
});

ws.on('error', (err) => {
  console.error('WS Error:', err);
  process.exit(1);
});
