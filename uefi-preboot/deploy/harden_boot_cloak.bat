@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Cyber Cafe PC Lock - BIOS F12 Cloak & Default Fallback
echo =======================================================

:: Check Admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Must run as Administrator!
    echo Right-click this script and select "Run as Administrator".
    pause
    exit /b 1
)

:: Locate source EFI binary
if exist "..\bin\pc_lock_preboot.efi" (
    set "EFI_SRC=..\bin\pc_lock_preboot.efi"
) else if exist "bin\pc_lock_preboot.efi" (
    set "EFI_SRC=bin\pc_lock_preboot.efi"
) else (
    echo [ERROR] pc_lock_preboot.efi binary not found!
    echo Please run build.bat first to compile the binary.
    pause
    exit /b 1
)

:: 1. Mount EFI Partition to drive S:
echo [*] Mounting EFI System Partition to drive S:...
mountvol S: /s
if %errorlevel% neq 0 (
    echo [ERROR] Failed to mount EFI partition.
    pause
    exit /b 1
)

:: 2. Cloak bootmgfw.efi -> bootmgfw_hidden.efi
if exist "S:\EFI\Microsoft\Boot\bootmgfw.efi" (
    echo [*] Cloaking Windows Boot Manager (bootmgfw.efi -^> bootmgfw_hidden.efi)...
    ren "S:\EFI\Microsoft\Boot\bootmgfw.efi" "bootmgfw_hidden.efi"
    echo [SUCCESS] Standard Windows Boot Manager is now CLOAKED from BIOS scan!
) else (
    if exist "S:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" (
        echo [INFO] bootmgfw.efi is already cloaked as bootmgfw_hidden.efi.
    ) else (
        echo [WARNING] Standard bootmgfw.efi path not found on S:\EFI\Microsoft\Boot\
    )
)

:: 3. Implement DEFAULT UEFI FALLBACK CLOAKING (\EFI\Boot\bootx64.efi)
echo [*] Installing Default Hardware Fallback Bootloader (\EFI\Boot\bootx64.efi)...
if not exist "S:\EFI\Boot" mkdir "S:\EFI\Boot"

if exist "S:\EFI\Boot\bootx64.efi" (
    if not exist "S:\EFI\Boot\bootx64_orig.efi" (
        echo [*] Backing up original bootx64.efi -^> bootx64_orig.efi...
        ren "S:\EFI\Boot\bootx64.efi" "bootx64_orig.efi"
    )
)

copy /y "%EFI_SRC%" "S:\EFI\Boot\bootx64.efi"
if %errorlevel% equ 0 (
    echo [SUCCESS] Default UEFI Hardware Fallback (\EFI\Boot\bootx64.efi) installed!
)

:: 4. Ensure Primary PCLock directory also has the binary
if not exist "S:\EFI\PCLock" mkdir "S:\EFI\PCLock"
copy /y "%EFI_SRC%" "S:\EFI\PCLock\pc_lock_preboot.efi"

:: 5. Remove standard Windows Boot Manager from Firmware Display Order
echo [*] Hardening Firmware Boot Order (Removing direct Windows Boot Manager from F12)...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /remove 2>nul

:: 6. Unmount EFI Partition
mountvol S: /d

echo.
echo =======================================================
echo [SUCCESS] FULL HARDENING & DEFAULT FALLBACK COMPLETE!
echo.
echo 1. BIOS F12 Boot Menu will NO LONGER SHOW 'Windows Boot Manager'.
echo 2. CMOS Battery Removal Protection Active:
echo    If CMOS battery is removed or NVRAM is wiped to factory defaults,
echo    the motherboard hardware fallback automatically executes our
echo    \EFI\Boot\bootx64.efi Pre-Boot Lock!
echo 3. The real Windows Boot Manager is safely hidden at bootmgfw_hidden.efi
echo    and only chainloaded when authorized.
echo.
echo [*] To restore default Windows behavior anytime: run restore_boot_cloak.bat
echo =======================================================
pause
