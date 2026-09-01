# 🛡️ Mode 2: Physical SPI Flash ROM Hardware Injection Guide

This guide explains how to permanently inject the **ACPI WPBT (Windows Platform Binary Table)** directly into your motherboard's physical SPI Flash BIOS ROM chip.

---

## ⚠️ Important Prerequisites
* **Target Scenario:** Enterprise Anti-Theft, Hardware-level Persistence (Level 5 Security).
* **Motherboard Requirement:** Dual BIOS, Flashback button, or an external hardware SPI programmer (CH341A / SOIC8 Clip) in case of recovery.

---

## 🛠️ Injection Methods

### Method A: UEFITool / MMTool (Software Capsule Injection)
1. Download your motherboard's official BIOS ROM dump or update file (`.bin` / `.rom` / `.cap`).
2. Open **UEFITool** (or AMI MMTool).
3. Search for the main **DXE Driver Firmware Volume (FV_MAIN / DXE)**.
4. Right-click on the Volume and choose **"Insert FFS File"** -> select `wpbt_dxe.ffs` (or insert `wpbt.aml` under ACPI Tables section).
5. Save the modified ROM image as `bios_modded.bin`.
6. Flash the modified BIOS using your motherboard's **BIOS Flashback** port or AFUWIN.

---

### Method B: External Hardware Programmer (CH341A / Flashrom - 100% Guaranteed)
1. Power off the computer and unplug the power cable.
2. Connect the **SOIC8 Test Clip** of the CH341A USB Programmer to the 8-pin SPI BIOS chip on the motherboard.
3. On another computer, run:
   ```bash
   flashrom -p ch341a_spi -r original_backup.bin
   ```
4. Modify `original_backup.bin` by inserting `wpbt_dxe.ffs` using UEFITool.
5. Flash back to the chip:
   ```bash
   flashrom -p ch341a_spi -w modded_bios.bin
   ```

---

## 🚀 Verification in Windows
Once booted into Windows:
1. Open PowerShell as Administrator and run:
   ```powershell
   Get-WmiObject -Namespace root\wmi -Class WmiMonitorID
   ```
2. Check `C:\Windows\System32\wpbbin.exe`. The Windows kernel (`ntoskrnl.exe`) will have automatically extracted and executed the binary directly from the physical motherboard ROM!
