const crypto = require('crypto');
const WebSocket = require('ws');

const pcWs = new WebSocket('wss://pc-lock.onrender.com/?device_id=pc_test_relay&device_type=PC');
pcWs.on('open', () => {
  console.log('PC connected to Render');
  
  // Now connect mobile
  const { privateKey, publicKey } = crypto.generateKeyPairSync('ec', {
    namedCurve: 'prime256v1',
    publicKeyEncoding: { type: 'spki', format: 'der' },
    privateKeyEncoding: { type: 'pkcs8', format: 'der' }
  });

  const mobWs = new WebSocket('wss://pc-lock.onrender.com/?device_id=mob_test_relay&device_type=MOBILE');
  mobWs.on('open', () => {
    console.log('Mobile connected to Render');
    
    const version = '1.0';
    const commandId = 'cmd_' + Date.now();
    const sender = 'mob_test_relay';
    const target = 'pc_test_relay';
    const action = 'LOCK_PC';
    const timestamp = Math.floor(Date.now() / 1000);
    const nonce = crypto.randomBytes(16).toString('hex');

    const canonical = version + ':' + commandId + ':' + sender + ':' + target + ':' + action + ':' + timestamp + ':' + nonce;
    const sign = crypto.createSign('SHA256');
    sign.update(canonical);
    sign.end();
    const signature = sign.sign({ key: crypto.createPrivateKey({ key: privateKey, format: 'der', type: 'pkcs8' }), dsaEncoding: 'ieee-p1363' }).toString('hex');

    const payload = {
      version,
      command_id: commandId,
      sender_device_id: sender,
      target_pc_id: target,
      action,
      timestamp,
      nonce,
      signature,
      public_key: publicKey.toString('hex')
    };

    console.log('Mobile sending LOCK_PC to PC...');
    mobWs.send(JSON.stringify(payload));
  });

  mobWs.on('message', (msg) => {
    console.log('Mobile received response:', msg.toString());
  });
});

pcWs.on('message', (msg) => {
  console.log('>>> PC RECEIVED MESSAGE FROM RELAY! <<<', msg.toString());
  setTimeout(() => process.exit(0), 1000);
});

pcWs.on('error', console.error);
