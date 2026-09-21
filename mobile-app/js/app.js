let RELAY_WS_URL =
  localStorage.getItem('pc_lock_server_url') ||
  (window.location.protocol === 'https:'
    ? 'wss://' + window.location.host
    : 'ws://' + (window.location.hostname || 'localhost') + ':4000');

let ws = null;
let registeredPcs = [];
let currentPcId = '';
let isLocked = false;

function getApiBaseUrl() {
  let url = RELAY_WS_URL;
  if (url.startsWith('wss://')) return url.replace('wss://', 'https://');
  if (url.startsWith('ws://')) return url.replace('ws://', 'http://');
  return url;
}

function configureCloudServer() {
  let current = localStorage.getItem('pc_lock_server_url') || RELAY_WS_URL;
  let newUrl = prompt('Enter Cloud Relay URL (e.g., https://pc-lock-relay.onrender.com or ws://localhost:4000):', current);
  if (newUrl && newUrl.trim() !== '') {
    newUrl = newUrl.trim();
    if (newUrl.startsWith('https://')) newUrl = newUrl.replace('https://', 'wss://');
    else if (newUrl.startsWith('http://')) newUrl = newUrl.replace('http://', 'ws://');
    else if (!newUrl.startsWith('ws://') && !newUrl.startsWith('wss://')) newUrl = 'wss://' + newUrl;

    RELAY_WS_URL = newUrl;
    localStorage.setItem('pc_lock_server_url', RELAY_WS_URL);
    if (ws) ws.close();
    connectRelay();
    fetchPcList();
  }
}

async function fetchPcList() {
  try {
    const apiBase = getApiBaseUrl();
    const res = await fetch(`${apiBase}/api/devices/status`);
    if (res.ok) {
      const data = await res.json();
      registeredPcs = data.pcs || [];
      renderPcDropdown();
    }
  } catch (e) {
    addLog('Failed to fetch PC list: ' + e.message, 'WARN');
  }
}

function renderPcDropdown() {
  const select = document.getElementById('pcSelector');
  if (!select) return;

  if (registeredPcs.length === 0) {
    select.innerHTML = '<option value="">No PCs connected yet (Run PC Agent)</option>';
    updateSelectedPcView(null);
    return;
  }

  select.innerHTML = registeredPcs
    .map(
      (p) => `
      <option value="${p.id}" ${p.id === currentPcId ? 'selected' : ''}>
        ${p.pc_number} - ${p.device_name} (${p.is_online ? '🟢 ONLINE' : '⚪ OFFLINE'})
      </option>
    `
    )
    .join('');

  if (!currentPcId || !registeredPcs.find((p) => p.id === currentPcId)) {
    currentPcId = registeredPcs[0].id;
  }
  const selected = registeredPcs.find((p) => p.id === currentPcId);
  updateSelectedPcView(selected);
}

function onTerminalSelect(pcId) {
  currentPcId = pcId;
  const selected = registeredPcs.find((p) => p.id === pcId);
  updateSelectedPcView(selected);
}

function updateSelectedPcView(pc) {
  if (!pc) {
    document.getElementById('pcNameDisplay').innerText = 'No PC Selected';
    document.getElementById('pcIdDisplay').innerText = 'Start PC Agent to connect';
    document.getElementById('pcOnlineBadge').className = 'badge badge-offline';
    document.getElementById('pcOnlineBadge').innerText = '⚪ OFFLINE';
    return;
  }

  document.getElementById('pcNameDisplay').innerText = `${pc.pc_number} (${pc.device_name})`;
  document.getElementById('pcIdDisplay').innerText = `ID: ${pc.id}`;
  document.getElementById('adminPinDisplay').innerText = pc.admin_pin || '998877';
  document.getElementById('lastActiveDisplay').innerText = pc.last_seen_at
    ? new Date(pc.last_seen_at).toLocaleTimeString()
    : pc.is_online
    ? 'Live'
    : 'Never';

  const badge = document.getElementById('pcOnlineBadge');
  if (pc.is_online) {
    badge.className = 'badge badge-online';
    badge.innerText = '🟢 ONLINE';
  } else {
    badge.className = 'badge badge-offline';
    badge.innerText = '⚪ OFFLINE';
  }

  updateLockUI(pc.lock_status === 'LOCKED');
}

function connectRelay() {
  try {
    ws = new WebSocket(`${RELAY_WS_URL}?device_id=${MOBILE_DEVICE_ID}&device_type=MOBILE`);
  } catch (e) {
    return;
  }

  ws.onopen = () => {
    addLog('Relay Gateway Connected', 'ONLINE');
    const pulse = document.getElementById('serverPulse');
    if (pulse) pulse.className = 'pulse-dot';
    fetchPcList();
  };

  ws.onmessage = (event) => {
    try {
      const data = JSON.parse(event.data);

      if (data.event === 'PC_CONNECTION_STATE') {
        const pc = registeredPcs.find((p) => p.id === data.pc_id);
        if (pc) {
          pc.is_online = data.is_online;
          pc.last_seen_at = data.last_seen_at;
        } else {
          fetchPcList();
        }
        renderPcDropdown();
        addLog(`PC ${data.pc_id} is now ${data.is_online ? 'ONLINE' : 'OFFLINE'}`, 'STATUS');
      } else if (data.event === 'PC_STATUS_UPDATED') {
        const pc = registeredPcs.find((p) => p.id === data.pc_id);
        if (pc) pc.lock_status = data.lock_status;
        if (currentPcId === data.pc_id) {
          updateLockUI(data.lock_status === 'LOCKED');
        }
        addLog(`PC ${data.pc_id} Lock State: ${data.lock_status}`, 'EVENT');
      } else if (data.event === 'PC_DEREGISTERED') {
        registeredPcs = registeredPcs.filter((p) => p.id !== data.pc_id);
        if (currentPcId === data.pc_id) currentPcId = '';
        renderPcDropdown();
        addLog('PC Uninstalled & Removed from Supabase Database', 'UNINSTALLED');
      } else if (data.status === 'REJECTED') {
        addLog(`Rejected: ${data.message}`, 'ERROR');
        alert('Security Error: ' + data.message);
      } else if (data.status === 'OFFLINE') {
        addLog(data.message, 'OFFLINE');
        alert(data.message);
      }
    } catch (e) {}
  };

  ws.onclose = () => {
    const pulse = document.getElementById('serverPulse');
    if (pulse) pulse.className = 'pulse-dot offline';
    setTimeout(connectRelay, 3000);
  };
}

async function sendSignedCommand(action) {
  if (!currentPcId) {
    alert('No PC connected. Please start the PC Security Agent first.');
    return;
  }

  const payload = await createSignedCommandPayload(currentPcId, action);

  if (ws && ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(payload));
    addLog(`Dispatched Signed ${action} -> ${currentPcId}`, 'SENT');
  } else {
    addLog('Failed: WebSocket Offline', 'ERROR');
  }
}

function onActionButtonClick() {
  if (!isLocked) {
    sendSignedCommand('LOCK_PC');
    updateLockUI(true);
  } else {
    if (window.AndroidBiometric && window.AndroidBiometric.triggerBiometricAuth) {
      window.AndroidBiometric.triggerBiometricAuth();
    } else {
      document.getElementById('bioModal').style.display = 'flex';
    }
  }
}

function approveBiometrics() {
  document.getElementById('bioModal').style.display = 'none';
  sendSignedCommand('UNLOCK_PC');
  updateLockUI(false);
  addLog('Biometrics Authorization Approved', 'AUTH_OK');
}

function updateLockUI(locked) {
  isLocked = locked;
  const textEl = document.getElementById('lockStateText');
  const btn = document.getElementById('actionBtn');
  if (!textEl || !btn) return;

  if (locked) {
    textEl.className = 'state-value locked';
    textEl.innerText = 'LOCKED';
    btn.className = 'action-btn btn-unlock';
    btn.innerHTML = '<span>🔓 UNLOCK PC</span>';
  } else {
    textEl.className = 'state-value unlocked';
    textEl.innerText = 'UNLOCKED';
    btn.className = 'action-btn btn-lock';
    btn.innerHTML = '<span>🔒 LOCK PC</span>';
  }
}

function addLog(msg, type) {
  const logBox = document.getElementById('auditLog');
  if (!logBox) return;
  const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  const item = document.createElement('div');
  item.className = 'log-item';
  item.innerHTML = `<span>${msg}</span><span>${time}</span>`;
  logBox.prepend(item);
}

// Initialization Lifecycle
window.addEventListener('DOMContentLoaded', () => {
  loadOrGenerateHardwareKeys(addLog).then(() => {
    fetchPcList();
    connectRelay();
  });
  setInterval(fetchPcList, 4000);
});
