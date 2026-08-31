# ⚡ ACPI WPBT (Windows Platform Binary Table) Format-Persistent Security

This subsystem implements Microsoft's official **ACPI WPBT (Windows Platform Binary Table)** specification to guarantee that the PC Security & Cyber Cafe Lock System **survives disk formatting, full SSD repartitioning, and fresh Windows 10/11 reinstallation.**

---

## 🔬 How It Works (The Science of WPBT)

1. **Motherboard ACPI Table Registration**:
   * The motherboard's ACPI tables publish a table with signature `'WPBT'` (0x54425057).
   * Inside the table, `HandoffMemoryLocation` points to physical RAM where the native executable (`wpbbin_agent.exe`) is mapped.
2. **Early Windows Kernel Boot Phase**:
   * During early boot (before user session or desktop loads), Windows kernel (`ntoskrnl.exe` / `smss.exe`) reads the ACPI `'WPBT'` table.
   * Windows automatically extracts the binary from motherboard memory and writes it to `%SystemRoot%\System32\wpbbin.exe`.
3. **Execution with SYSTEM Privileges**:
   * Windows invokes `wpbbin.exe` with `NT AUTHORITY\SYSTEM` privileges.
   * `wpbbin.exe` enforces the registry lock state (`HKLM\SOFTWARE\PCSecuritySystem`), locks the workstation session, and resurrects the full PC Security Agent service.

---

## 📁 Directory Layout

* `src/wpbt_table.h`: Official Microsoft ACPI WPBT data structure definition.
* `src/wpbbin_agent.c`: Native C payload dropped and executed by the Windows Kernel.
* `src/wpbt_injector.c`: Standalone UEFI application to publish the WPBT table in ACPI memory on boot.
* `asl/wpbt.asl`: Intel ACPI Source Language (ASL) source code for direct BIOS ROM flashing.
* `build.bat`: One-click compiler for all WPBT binaries.
* `deploy/install_wpbt.bat`: Verification and deployment utility.

---

## 🚀 How to Build and Deploy

### 1. Build Binaries
Run `build.bat` inside the `acpi-wpbt` folder (requires Clang: `winget install LLVM.LLVM`).
Outputs:
* `bin\wpbbin_agent.exe`
* `bin\wpbt_injector.efi`

### 2. Verify Execution
Run `deploy\install_wpbt.bat` as Administrator. Check `C:\Windows\Temp\wpbt_boot_log.txt` to see the kernel resurrection logs.
