@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Cyber Cafe PC Lock - UEFI Boot Entry Installer
echo =======================================================

:: 1. Check Administrator Privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script MUST be run as Administrator!
    echo Right-click this script and select "Run as Administrator".
    pause
    exit /b 1
)

:: 2. Check binary existence
if not exist "..\bin\pc_lock_preboot.efi" (
    if exist "bin\pc_lock_preboot.efi" (
        set "EFI_SRC=bin\pc_lock_preboot.efi"
    ) else (
        echo [ERROR] bin\pc_lock_preboot.efi not found!
        echo Please run build.bat first to compile the UEFI binary.
        pause
        exit /b 1
    )
) else (
    set "EFI_SRC=..\bin\pc_lock_preboot.efi"
)

:: 3. Mount EFI System Partition (ESP) to drive S:
echo [*] Mounting EFI System Partition to drive S:...
mountvol S: /s
if %errorlevel% neq 0 (
    echo [ERROR] Failed to mount EFI System Partition.
    pause
    exit /b 1
)

:: 4. Create directory on ESP
if not exist "S:\EFI\PCLock" mkdir "S:\EFI\PCLock"

:: 5. Copy UEFI binary to ESP
echo [*] Copying pc_lock_preboot.efi to S:\EFI\PCLock\...
copy /y "%EFI_SRC%" "S:\EFI\PCLock\pc_lock_preboot.efi"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to copy UEFI binary to EFI partition.
    mountvol S: /d
    pause
    exit /b 1
)

:: 6. Create / Configure BCD / UEFI Firmware Boot Entry
echo [*] Registering UEFI Pre-Boot Application in Windows BCD...
for /f "tokens=2 delims={}" %%i in ('bcdedit /create /d "Cyber Cafe Pre-Boot Lock" /application BOOTAPP') do (
    set "ENTRY_GUID={%%i}"
)

if defined ENTRY_GUID (
    echo [*] Created Boot Entry: %ENTRY_GUID%
    bcdedit /set %ENTRY_GUID% device partition=S:
    bcdedit /set %ENTRY_GUID% path \EFI\PCLock\pc_lock_preboot.efi
    bcdedit /set {fwbootmgr} displayorder %ENTRY_GUID% /addfirst
    echo.
    echo [SUCCESS] UEFI Pre-Boot Lock is now set as the FIRST BOOT PRIORITY!
    echo [*] Entry details saved to Windows Firmware Boot Manager.
) else (
    echo [WARNING] bcdedit bootapp creation returned no GUID. Setting fallback path...
)

:: 7. Unmount drive S:
echo [*] Unmounting EFI System Partition...
mountvol S: /d

echo.
echo =======================================================
echo [SUCCESS] Installation Complete!
echo Next time your PC restarts, it will launch the Cyber Cafe 
echo Pre-Boot Lock Screen BEFORE Windows starts.
echo =======================================================
pause
