# 🛡️ PC Security System & Cyber Cafe Lock — Security Audit & Vulnerability Report

**Document Version:** 1.0.0  
**Target Repository:** `PC_Lock`  
**Audit Scope:** UEFI Pre-Boot Firmware (`uefi-preboot`), ACPI WPBT Dropper (`acpi-wpbt`), Windows Agent Service (`pc-agent`), Cloud Relay Server (`backend`), and Mobile Client (`mobile-app`).  
**Classification:** Internal Technical Audit & Hardening Guide

---

## 📊 1. Executive Summary & Vulnerability Scorecard

| ID | Vulnerability Title | Subsystem | Severity | Status |
| :--- | :--- | :--- | :--- | :--- |
| **SEC-01** | Hardcoded Keyboard Bypass Sequence (`s` → `h` → `j`) | `uefi-preboot` | 🚨 **CRITICAL** | Action Required |
| **SEC-02** | Un-overridable Default Admin PIN (`998877`) | `uefi-preboot` | 🚨 **CRITICAL** | Action Required |
| **SEC-03** | Bypassed Signature Validation in Windows Agent | `pc-agent` | 🚨 **CRITICAL** | Action Required |
| **SEC-04** | Broken WebCrypto Property & Hardcoded Static Signature | `mobile-app` | 🚨 **CRITICAL** | Action Required |
| **SEC-05** | Unauthenticated State-Mutating Endpoints | `backend` | 🔴 **HIGH** | Action Required |
| **SEC-06** | Plaintext Admin PIN Leak in Pre-Boot Status API | `backend` | 🔴 **HIGH** | Action Required |
| **SEC-07** | Stubbed Non-Functional Network Query in Pre-Boot | `uefi-preboot` | 🔴 **HIGH** | Action Required |
| **SEC-08** | Off-by-One Buffer Overflow in Admin PIN Copy | `uefi-preboot` | 🔴 **HIGH** | Action Required |
| **SEC-09** | Incomplete C++ Credential Provider (`E_NOTIMPL`) | `pc-agent` | 🟡 **MEDIUM** | Action Required |
| **SEC-10** | Hardcoded Drive Letter `S:` Collision & Force-Unmount | `pc-agent` | 🟡 **MEDIUM** | Action Required |
| **SEC-11** | Silent WebSocket Connection Loss (No Heartbeat) | `pc-agent` | 🟡 **MEDIUM** | Action Required |
| **SEC-12** | Transient RAM ACPI WPBT Table Injection Persistence Limit | `acpi-wpbt` | 🟡 **MEDIUM** | Informational |

---

## 🚨 2. Critical Vulnerabilities & Backdoors

### SEC-01: Hardcoded Keyboard Bypass Sequence in Pre-Boot
* **File:** [`uefi-preboot/src/efi_main.c`](file:///D:/Soft/PC_Lock/uefi-preboot/src/efi_main.c#L91-L108)
* **Vulnerable Code:**
  ```c
  if ((Key.UnicodeChar == 0x13 || Key.UnicodeChar == L's' || Key.UnicodeChar == L'S') && SecretSequenceStep == 0) {
      SecretSequenceStep = 1;
  } else if ((Key.UnicodeChar == L'h' || Key.UnicodeChar == L'H') && SecretSequenceStep == 1) {
      SecretSequenceStep = 2;
  } else if ((Key.UnicodeChar == L'j' || Key.UnicodeChar == L'J') && SecretSequenceStep == 2) {
      IsUnlocked = TRUE; // Direct unlock without PIN or server check!
  }
  ```
* **Vulnerability Description:** Any person physically present at any workstation who types `s` → `h` → `j` on the keyboard will instantly unlock the pre-boot screen and boot into Windows.
* **Remediation:** Remove the entire hardcoded secret sequence logic.

---

### SEC-02: Un-overridable Default Admin PIN (`998877`)
* **File:** [`uefi-preboot/src/efi_main.c`](file:///D:/Soft/PC_Lock/uefi-preboot/src/efi_main.c#L7)
* **Vulnerable Code:**
  ```c
  #define DEFAULT_ADMIN_PIN L"998877"
  ...
  if (StringEquals(EnteredDigits, ActiveAdminPin) || StringEquals(EnteredDigits, DEFAULT_ADMIN_PIN))
  ```
* **Vulnerability Description:** Because of the logical `||` operator, entering `998877` will ALWAYS unlock every terminal, even after the owner sets custom PINs via the dashboard.
* **Remediation:** Remove `|| StringEquals(EnteredDigits, DEFAULT_ADMIN_PIN)` so only `ActiveAdminPin` is authenticated.

---

### SEC-03: Bypassed Signature Validation in PC Agent
* **File:** [`pc-agent/Security/CommandValidator.cs`](file:///D:/Soft/PC_Lock/pc-agent/Security/CommandValidator.cs#L64-L74)
* **Vulnerable Code:**
  ```csharp
  byte[] sigBytes = Convert.FromHexString(payload.Signature);
  if (sigBytes.Length > 0)
  {
      return (true, "Signature verified"); // ⚠️ No actual cryptographic verification!
  }
  ```
* **Vulnerability Description:** The agent only checks if the signature string is non-empty. Any unauthorized actor on the network can send arbitrary JSON payloads with `signature: "00"` and lock or unlock workstations.
* **Remediation:** Implement genuine Ed25519/ECDsa cryptographic verification against the registered mobile public key stored in TPM or secure config.

---

### SEC-04: Broken WebCrypto Property & Hardcoded Static Signature in Mobile App
* **File:** [`mobile-app/index.html`](file:///D:/Soft/PC_Lock/mobile-app/index.html#L161)
* **Vulnerable Code:**
  ```javascript
  // Typo: window.crypto.subcrypto does not exist (Standard: window.crypto.subtle)
  mobileKeyPair = await window.crypto.subcrypto?.generateKey(...);
  ...
  // Hardcoded static signature:
  signature: "3b7f8c9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e"
  ```
* **Vulnerability Description:** WebCrypto key generation fails silently due to the typo. The mobile app always transmits a fixed mock signature string.
* **Remediation:** Fix property to `window.crypto.subtle` and sign the canonical payload string using `crypto.subtle.sign()`.

---

## 🔴 3. High Severity Issues (Backend & Firmware)

### SEC-05: Unauthenticated State-Mutating Endpoints
* **File:** [`backend/src/index.ts`](file:///D:/Soft/PC_Lock/backend/src/index.ts)
* **Vulnerability Description:** Endpoints `/api/preboot/toggle` and `/api/devices/pc/set-pin` have no JWT authentication middleware. Any local or remote user can toggle lock states or overwrite admin PINs without credentials.
* **Remediation:** Attach authentication middleware verifying the admin JWT Bearer token before processing state mutations.

---

### SEC-06: Plaintext Admin PIN Leak in Pre-Boot Status API
* **File:** [`backend/src/index.ts`](file:///D:/Soft/PC_Lock/backend/src/index.ts)
* **Vulnerable Code:**
  ```typescript
  res.json({
    status: 'SUCCESS',
    preboot_authorized: !isLocked,
    lock_status: pc?.lock_status || 'LOCKED',
    admin_pin: pc?.admin_pin || '998877', // ⚠️ Leaking secret PIN in plaintext!
    ...
  });
  ```
* **Vulnerability Description:** Querying `GET /api/preboot/status?pc_id=pc_dev_01` returns the workstation's private emergency PIN in the plaintext response body.
* **Remediation:** Omit `admin_pin` from all public read responses.

---

### SEC-07: Stubbed Non-Functional Network Query in Pre-Boot
* **File:** [`uefi-preboot/src/network.c`](file:///D:/Soft/PC_Lock/uefi-preboot/src/network.c#L70-L76)
* **Vulnerable Code:**
  ```c
  PREBOOT_LOCK_STATE QueryPreBootLockStatus(EFI_SYSTEM_TABLE *ST, NETWORK_DEVICE_INFO *NetInfo)
  {
      return PREBOOT_STATE_LOCKED; // Always locked stub
  }
  ```
* **Vulnerability Description:** The firmware never executes network requests during boot. Remote unlock commands from phone or dashboard cannot unlock a booting PC over the network.
* **Remediation:** Implement `EFI_HTTP_PROTOCOL` or UDP broadcast polling to query server authorization status.

---

### SEC-08: Off-by-One Buffer Overflow in Admin PIN Copy
* **File:** [`uefi-preboot/src/efi_main.c`](file:///D:/Soft/PC_Lock/uefi-preboot/src/efi_main.c#L38)
* **Vulnerable Code:**
  ```c
  for (UINTN i = 0; i < MaxLen && NvramPin[i] != L'\0'; i++) {
      OutPin[i] = NvramPin[i];
      OutPin[i+1] = L'\0'; // ⚠️ Out of bounds write when i == MaxLen - 1
  }
  ```
* **Vulnerability Description:** If `NvramPin` has 16 characters, `OutPin[i+1]` writes to index 16 on an array sized 16 (`ActiveAdminPin[16]`), corrupting stack memory.
* **Remediation:** Remove `OutPin[i+1] = L'\0'` from inside the loop and null-terminate safely after loop exit: `OutPin[MaxLen - 1] = L'\0';`.

---

## 🟡 4. Medium Severity & Stability Risks

### SEC-09: Incomplete C++ Credential Provider (`E_NOTIMPL`)
* **File:** [`pc-agent/CredentialProvider/WinlogonProvider.cpp`](file:///D:/Soft/PC_Lock/pc-agent/CredentialProvider/WinlogonProvider.cpp#L72)
* **Impact:** `GetCredentialAt` returns `E_NOTIMPL`. Registering this DLL in the Windows Registry will cause `LogonUI.exe` to fail or crash at the Windows logon screen.
* **Remediation:** Implement the complete `ICredentialProviderCredential2` interface or utilize a full-screen kiosk lock overlay.

---

### SEC-10: Hardcoded Drive Letter `S:` Collision & Force-Unmount
* **File:** [`pc-agent/Controllers/BootGuardHealer.cs`](file:///D:/Soft/PC_Lock/pc-agent/Controllers/BootGuardHealer.cs)
* **Impact:** `RunProcess("mountvol", "S: /s")` hardcodes `S:`. If a user has a USB drive or partition mounted on `S:`, the healer will fail and force-unmount the user's drive `S:` in the `finally` block.
* **Remediation:** Dynamically allocate an unused drive letter before mounting.

---

### SEC-11: Silent WebSocket Connection Drops (No Heartbeat)
* **File:** [`pc-agent/Network/WssClient.cs`](file:///D:/Soft/PC_Lock/pc-agent/Network/WssClient.cs)
* **Impact:** If the network hiccups or the router restarts, TCP half-open connections leave `ReceiveAsync` hanging indefinitely without reconnecting.
* **Remediation:** Implement a 30-second ping/heartbeat loop to detect dead sockets and reconnect.

---

## 🛠️ 5. Step-by-Step Remediation Plan

1. **Firmware Hardening:**
   - [ ] Delete `s-h-j` keyboard sequence in `uefi-preboot/src/efi_main.c`.
   - [ ] Remove `DEFAULT_ADMIN_PIN` fallback check in `uefi-preboot/src/efi_main.c`.
   - [ ] Fix off-by-one buffer termination in `LoadActiveAdminPin`.
   - [ ] Implement `EFI_HTTP_PROTOCOL` network querying in `uefi-preboot/src/network.c`.

2. **PC Agent & Cryptography:**
   - [ ] Implement real Ed25519 signature verification in `pc-agent/Security/CommandValidator.cs`.
   - [ ] Add dynamic drive letter selection in `pc-agent/Controllers/BootGuardHealer.cs`.
   - [ ] Add 30s heartbeat timer in `pc-agent/Network/WssClient.cs`.

3. **Backend & Mobile App:**
   - [ ] Add JWT authentication middleware to `/api/preboot/toggle` and `/api/devices/pc/set-pin`.
   - [ ] Remove `admin_pin` from `/api/preboot/status` response.
   - [ ] Fix `window.crypto.subtle` and payload signing in `mobile-app/index.html`.

---
*Report generated on September 1, 2026 for PC_Lock Project Repository.*
