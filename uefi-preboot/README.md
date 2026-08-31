# 🛡️ Cyber Cafe Pre-Boot UEFI Security Controller

This subsystem provides true **Pre-Boot Operating System Interception** for Cyber Cafe workstations. It executes in the motherboard's UEFI firmware environment **before** Windows starts.

---

## 🔒 Hardened Anti-Tamper & Security Features

1. **Default Hardware Fallback Cloaking (`\EFI\Boot\bootx64.efi`)**:
   * Even if a customer physically removes the CMOS battery or shorts the BIOS jumper to reset NVRAM to factory defaults, UEFI firmware automatically executes the architectural fallback **`\EFI\Boot\bootx64.efi`** (which is our Pre-Boot Lock).
   * Real Windows Boot Manager is cloaked at `bootmgfw_hidden.efi`, making it impossible for factory-reset BIOS to boot Windows without authorization.
2. **Secret Key Bypass Combination (`Ctrl + Shift + S + H + J`)**:
   * Only the secret multi-key sequence `Ctrl+Shift+S+H+J` (or typing `S` -> `H` -> `J`) triggers the admin emergency bypass. Single-key `'U'` has been completely eliminated.
3. **BIOS F12 Windows Boot Manager Cloaking (`harden_boot_cloak.bat`)**:
   * Removes `{bootmgr}` from motherboard NVRAM firmware display order.
   * **Result**: Motherboard BIOS F12 menu **cannot** see or boot Windows directly.
4. **Custom Per-PC Admin Emergency PIN (Dynamic Sync)**:
   * Each terminal (PC-01, PC-02, PC-03...) can have a unique PIN set directly from the **Mobile App** or **Counter Web Dashboard**.

---

## 🚀 How to Build & Deploy

### Step 1: Compile the UEFI Binary
1. Open `uefi-preboot` and run `build.bat` (requires Clang: `winget install LLVM.LLVM`).
2. Generates `bin\pc_lock_preboot.efi`.

### Step 2: Install & Hard-Cloak Bootloader
1. Right-click `deploy\install_boot_entry.bat` -> **Run as Administrator**.
2. Right-click `deploy\harden_boot_cloak.bat` -> **Run as Administrator**.
   * *This sets up both NVRAM primary boot priority and default hardware fallback (`\EFI\Boot\bootx64.efi`), cloaking Windows from BIOS F12 and CMOS resets!*

### Step 3: Changing PINs from Counter / Mobile
* Open **`http://localhost:4000/`** or the Mobile App.
* Click **✏️ Edit** next to any PC's Emergency PIN to set a custom 6-digit PIN for that terminal.
