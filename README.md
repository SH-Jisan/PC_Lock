# 🛡️ Cyber Cafe Secure Workstation & Pre-Boot Interception System

[![Architecture](https://img.shields.io/badge/Architecture-5--Layer%20Deep%20Security-blue.svg)](#-system-architecture)
[![Pre-Boot](https://img.shields.io/badge/Firmware-UEFI%202.x%20%2B%20ACPI%20WPBT-orange.svg)](#-layer-1-uefi-pre-boot-subsystem)
[![Windows Agent](https://img.shields.io/badge/OS%20Agent-.NET%208%20Windows%20Service-green.svg)](#-layer-3-windows-pc-security-agent)
[![Backend](https://img.shields.io/badge/Backend-Node.js%20%7C%20WebSocket%20%7C%20Docker-brightgreen.svg)](#-layer-4-cloud-relay-hub)
[![Mobile](https://img.shields.io/badge/Mobile-Android%20APK%20%7C%20PWA-purple.svg)](#-layer-5-trusted-mobile-controller)

A military-grade, multi-tiered security, locking, and remote management system tailored specifically for **Cyber Cafes, Gaming Centers, and High-Security Workstations**. 

Unlike standard software lockers that only lock the Windows desktop screen, this system intercepts the machine **at the motherboard firmware level (UEFI Pre-Boot) before Windows even loads**, while maintaining real-time remote control over live sessions from a cloud-connected mobile phone.

---

## 📑 Table of Contents

- [🛡️ System Architecture](#-system-architecture)
- [✨ Key Security & Management Features](#-key-security--management-features)
- [📦 Subsystem Breakdown](#-subsystem-breakdown)
- [🚀 Complete Step-by-Step Installation Manual](#-complete-step-by-step-installation-manual)
  - [Phase 1: Cloud Relay Server Deployment (24/7 Free)](#phase-1-cloud-relay-server-deployment-247-free)
  - [Phase 2: Client Workstation UEFI Pre-Boot Installation](#phase-2-client-workstation-uefi-pre-boot-installation)
  - [Phase 3: Client Workstation Windows Background Service Installation](#phase-3-client-workstation-windows-background-service-installation)
  - [Phase 4: Mobile Admin Controller APK Setup](#phase-4-mobile-admin-controller-apk-setup)
- [🎮 Administrator Daily Operation Guide](#-administrator-daily-operation-guide)
- [🚨 Emergency Offline Bypass Procedures](#-emergency-offline-bypass-procedures)
- [🔒 Enterprise Hardening & Defense Recommendations](#-enterprise-hardening--defense-recommendations)
- [📁 Project Repository Structure](#-project-repository-structure)

---

## 🛡️ System Architecture

```
                                  ┌──────────────────────────────────────────────────┐
                                  │      📱 ADMIN MOBILE PHONE CONTROLLER (APK)       │
                                  │  • Biometric Touch / Custom Terminal Selector    │
                                  │  • Ed25519 Cryptographic Command Signer          │
                                  └─────────────────────────┬────────────────────────┘
                                                            │ (Secure WebSocket / HTTPS)
                                                            ▼
                                  ┌──────────────────────────────────────────────────┐
                                  │       ☁️ 24/7 CLOUD RELAY SERVER (Docker)        │
                                  │  • Real-Time Gateway & Multi-Terminal Routing    │
                                  │  • Counter Web Dashboard & Audit Logger          │
                                  └────────────┬────────────────────────┬────────────┘
                                               │                        │
                     ┌─────────────────────────┘                        └─────────────────────────┐
                     ▼                                                                            ▼
┌──────────────────────────────────────────────┐                            ┌──────────────────────────────────────────────┐
│       WORKSTATION 1 (TERMINAL PC-01)         │                            │       WORKSTATION 2 (TERMINAL PC-02)         │
├──────────────────────────────────────────────┤                            ├──────────────────────────────────────────────┤
│ 1. Motherboard UEFI Pre-Boot Lock Screen     │                            │ 1. Motherboard UEFI Pre-Boot Lock Screen     │
│ 2. BIOS F12 Boot Cloaking & Hardware Fallback│                            │ 2. BIOS F12 Boot Cloaking & Hardware Fallback│
│ 3. ACPI WPBT Kernel Dropper & Self-Healing   │                            │ 3. ACPI WPBT Kernel Dropper & Self-Healing   │
│ 4. Windows Background Service (.NET 8)       │                            │ 4. Windows Background Service (.NET 8)       │
│ 5. Winlogon Credential Provider Interceptor  │                            │ 5. Winlogon Credential Provider Interceptor  │
└──────────────────────────────────────────────┘                            └──────────────────────────────────────────────┘
```

---

## ✨ Key Security & Management Features

1. **Motherboard Pre-Boot Operating System Lock**:
   * Blocks the computer at the firmware level before Windows starts.
   * Customers cannot access Windows files, launch games, or bypass security by power-cycling.
2. **Instant Live Session Remote Lock**:
   * If a customer engages in unauthorized or harmful activity, the admin can tap **`🔒 LOCK`** on their phone to immediately freeze the Windows session within sub-seconds via `LockWorkStation` and disable local logon tiles.
3. **BIOS F12 Menu Cloaking & Default Hardware Fallback (`\EFI\Boot\bootx64.efi`)**:
   * Windows Boot Manager (`bootmgfw.efi`) is cloaked to `bootmgfw_hidden.efi` and hidden from the motherboard F12 boot selection menu.
   * Even if a customer opens the PC case and removes the CMOS battery to reset the BIOS, the UEFI specification default fallback automatically executes our pre-boot lock!
4. **3-Stage Self-Healing Update Protection**:
   * **Stage 1**: Continuous 5-minute background auto-healer in PC Agent.
   * **Stage 2**: Windows Pre-Shutdown & Reboot Event Hook (`ProcessExit`) that repairs EFI cloaking before Windows restarts.
   * **Stage 3**: ACPI WPBT Kernel Dropper that resurrects and re-cloaks on fresh boots or full SSD format.
5. **Configurable Dynamic Per-Terminal Emergency PINs**:
   * Every PC can have a distinct 6-digit emergency PIN configurable on the fly from the Mobile App or Reception Dashboard.
6. **Secret Admin Multi-Key Bypass Sequence**:
   * Single-key bypasses are eliminated. Only pressing the secret combination **`Ctrl + Shift + S + H + J`** (or typing `S` ➔ `H` ➔ `J`) unlocks the emergency pre-boot screen.

---

## 📦 Subsystem Breakdown

| Subsystem | Folder | Language / Tech | Purpose |
| :--- | :--- | :--- | :--- |
| **UEFI Pre-Boot** | [`uefi-preboot/`](file:///c:/Users/USER/Downloads/PC_Lock-main/PC_Lock-main/uefi-preboot/) | C (Freestanding UEFI 2.x) | Intercepts boot sequence, renders 1080p/4K lock screen via GOP, chainloads cloaked Windows when authorized. |
| **ACPI WPBT** | [`acpi-wpbt/`](file:///c:/Users/USER/Downloads/PC_Lock-main/PC_Lock-main/acpi-wpbt/) | C / ASL / Windows Native PE | Injects Microsoft WPBT table into motherboard ACPI RAM to resurrect lock agents even after SSD formatting. |
| **PC Agent** | [`pc-agent/`](file:///c:/Users/USER/Downloads/PC_Lock-main/PC_Lock-main/pc-agent/) | C# (.NET 8) / C++ Winlogon | Native Windows Service running under `SYSTEM` for real-time WebSocket connection, instant locking, and auto-healing. |
| **Backend Relay** | [`backend/`](file:///c:/Users/USER/Downloads/PC_Lock-main/PC_Lock-main/backend/) | Node.js / TypeScript / SQLite | Central relay gateway, REST API, Reception counter live terminal grid, and Ed25519 signature verifier. |
| **Mobile App** | [`mobile-app/`](file:///c:/Users/USER/Downloads/PC_Lock-main/PC_Lock-main/mobile-app/) | HTML5 / JavaScript / PWA / APK | Trusted phone controller with terminal selector, biometric verification, instant lock/unlock, and PIN editor. |

---

## 🚀 Complete Step-by-Step Installation Manual

### Phase 1: Cloud Relay Server Deployment (24/7 Free)

The backend server acts as the bridge connecting your phone and cyber cafe workstations across any network.

1. Create a free account on **[Render.com](https://render.com/)** or **[Railway.app](https://railway.app/)**.
2. Click **New +** ➔ **Web Service** ➔ Connect your GitHub Repository.
3. Set the following settings:
   * **Root Directory**: `backend`
   * **Environment / Runtime**: `Docker`
   * **Instance Type**: `Free`
4. Click **Deploy Web Service**.
5. Once deployed, note down your live HTTPS/WSS URL:
   * Example: `https://my-cyber-relay.onrender.com` (WebSocket: `wss://my-cyber-relay.onrender.com`)
   * *You can access the Reception Counter Web Dashboard anytime by opening this URL in any browser!*

---

### Phase 2: Client Workstation UEFI Pre-Boot Installation

*(Perform once on each cyber cafe client PC)*

1. Ensure **Secure Boot is Disabled** in the motherboard BIOS settings.
2. Open the `uefi-preboot` directory and run **`build.bat`** (requires Clang / LLVM).
3. Right-click **`deploy\install_boot_entry.bat`** ➔ **Run as Administrator**.
4. Right-click **`deploy\harden_boot_cloak.bat`** ➔ **Run as Administrator**.
   * *This cloaks the real Windows Boot Manager and installs the default hardware fallback (`\EFI\Boot\bootx64.efi`).*

---

### Phase 3: Client Workstation Windows Background Service Installation

*(Perform once on each cyber cafe client PC)*

1. In the `pc-agent` directory, double-click **`publish_single_file.bat`**.
   * *This compiles the entire agent into a standalone executable: `bin_publish\PC.SecurityAgent.exe`.*
2. Set your Cloud Relay URL by running this in Administrator Command Prompt:
   ```cmd
   setx PC_SECURITY_RELAY_URL "wss://my-cyber-relay.onrender.com" /M
   setx PC_SECURITY_DEVICE_ID "pc_dev_01" /M
   ```
   *(Change `pc_dev_01` to `pc_dev_02`, `pc_dev_03` for respective workstations).*
3. Right-click **`install_service.bat`** ➔ **Run as Administrator**.
   * *The PC Security Agent is now installed as a permanent Windows Service (`PCSecurityAgentService`).*
   * *It will start automatically on every Windows boot. You never have to touch or open it again!*

---

### Phase 4: Mobile Admin Controller APK Setup

1. Bundle the [`mobile-app/`](file:///c:/Users/USER/Downloads/PC_Lock-main/PC_Lock-main/mobile-app/) folder into an Android APK using [Web2Apk](https://websitetoapk.com/), [Capacitor](https://capacitorjs.com/), or install as a Progressive Web App (PWA) on your phone.
2. Open the app on your mobile phone.
3. Tap the **`🌐 Server`** button in the top status bar.
4. Enter your live cloud URL (e.g., `https://my-cyber-relay.onrender.com`).
5. Your phone is now securely paired and connected!

---

## 🎮 Administrator Daily Operation Guide

### 1. Unlocking a PC for a Customer
* **From Mobile Phone**:
  1. Open the App and select the terminal from the dropdown (e.g. `TERMINAL [PC-01]`).
  2. Tap **`🔓 UNLOCK TERMINAL & BOOT`**.
  3. Touch fingerprint sensor / approve biometric prompt.
  4. The pre-boot lock on PC-01 instantly releases and boots into Windows!
* **From Reception Counter Dashboard**:
  1. Open `https://my-cyber-relay.onrender.com` in the counter browser.
  2. Click **`🔓 Unlock Boot`** on the respective workstation card.

### 2. Remotely Freezing an Active Session
* If a customer violates rules or their session time expires:
  1. Select the terminal in the Mobile App.
  2. Tap the red **`🔒 LOCK TERMINAL`** button.
  3. The workstation screen locks instantly, blocking all user input.

### 3. Changing Emergency PINs on the Fly
* Select the terminal in the Mobile App ➔ Click **`Edit`** next to `ADMIN EMERGENCY PIN` ➔ Enter a new 6-digit PIN ➔ Tap OK.

---

## 🚨 Emergency Offline Bypass Procedures

If the mobile app is unavailable or internet connectivity is temporarily down, the administrator can unlock any terminal using either of the following offline hardware bypass methods directly at the workstation keyboard:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ METHOD A: Custom Emergency 6-Digit PIN                                      │
│ • Simply type the terminal's 6-digit Emergency PIN on the keyboard          │
│ • Press Enter ➔ System unlocks and chainloads Windows OS.                   │
├─────────────────────────────────────────────────────────────────────────────┤
│ METHOD B: Secret Multi-Key Sequence                                         │
│ • Press and hold: Ctrl + Shift + S + H + J                                  │
│ • (Or type the sequence 'S' ➔ 'H' ➔ 'J' while holding Ctrl+Shift)           │
│ • Screen displays "[SECRET COMBO APPROVED]" and immediately boots Windows!  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 🔒 Enterprise Hardening & Defense Recommendations

To achieve 100% unbreakable security in a commercial cyber cafe environment, implement these three physical and BIOS configurations:

1. **Set Motherboard BIOS Supervisor Password**:
   * Prevents customers from accessing BIOS setup menus or changing boot priority.
2. **Enable BitLocker Drive Encryption (TPM 2.0 PCR Sealing)**:
   * Protects SSD data. If the motherboard BIOS is ever cleared or the SSD is moved to another computer, TPM locks the encryption key.
3. **Physical PC Case Lock**:
   * Use a small padlock or chassis intrusion switch on computer cabinets to prevent physical internal tampering.

---

## 📁 Project Repository Structure

```
PC_Lock-main/
├── uefi-preboot/                # 💾 Firmware Pre-Boot Engine
│   ├── include/uefi.h           # Freestanding UEFI 2.x protocol definitions
│   ├── src/efi_main.c           # Event loop, secret combo, dynamic PIN matcher
│   ├── src/graphics.c           # GOP 1080p/4K Cyber Lock screen renderer
│   ├── src/chainloader.c        # Cloaked Windows Boot Manager loader
│   ├── src/network.c            # UEFI Simple Network Protocol interface
│   ├── deploy/                  # BCD and BIOS F12 cloaking installation scripts
│   └── build.bat                # LLVM/Clang EFI compiler script
├── acpi-wpbt/                   # ⚡ Motherboard ACPI Firmware Dropper
│   ├── src/wpbt_table.h         # Microsoft WPBT v1.0 table specification
│   ├── src/wpbbin_agent.c       # Native kernel startup auto-relocker & healer
│   ├── src/wpbt_injector.c      # UEFI ACPI table publisher
│   └── asl/wpbt.asl             # Native ASL table source for ROM flashing
├── pc-agent/                    # 🖥️ Windows Background Service & Winlogon
│   ├── Controllers/             # LockController & BootGuardHealer auto-healer
│   ├── CredentialProvider/      # C++ Winlogon custom login tile interceptor
│   ├── Hardware/                # TPM 2.0 hardware key manager
│   ├── Network/                 # Resilient WebSocket relay client
│   ├── Services/                # BackgroundService with Pre-Shutdown hook
│   ├── install_service.bat      # 1-click permanent Windows service installer
│   └── publish_single_file.bat  # Standalone single .exe packager
├── backend/                     # 🌐 Central Cloud Relay Hub & REST API
│   ├── src/index.ts             # Express server & Counter Web Grid Dashboard
│   ├── src/gateway.ts           # WebSocket relay gateway & dynamic discovery
│   ├── src/crypto.ts            # Ed25519 signature & anti-replay validator
│   ├── src/db.ts                # SQLite database with custom PIN persistence
│   └── Dockerfile               # 1-click 24/7 cloud deployment configuration
└── mobile-app/                  # 📱 Trusted Mobile Controller (APK / PWA)
    ├── index.html               # Sleek cyber UI, terminal selector, biometric auth
    └── manifest.json            # Web App Manifest for mobile installation
```

---

## 📄 License & Compliance

Developed for authorized commercial cyber cafe administration, workstation fleet control, and educational firmware research.
