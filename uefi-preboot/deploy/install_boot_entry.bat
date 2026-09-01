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

:: 2. Locate source EFI binary using absolute script path
set "EFI_SRC=%~dp0..\bin\pc_lock_preboot.efi"
if not exist "%EFI_SRC%" (
    if exist "%~dp0pc_lock_preboot.efi" (
        set "EFI_SRC=%~dp0pc_lock_preboot.efi"
    ) else (
        echo [ERROR] pc_lock_preboot.efi not found at: %EFI_SRC%
        echo Please run build.bat first to compile the UEFI binary.
        pause
        exit /b 1
    )
)

:: 3. Find available drive letter
set MOUNT_LETTER=
for %%D in (Z Y X W V U T S R Q P) do (
    if not exist "%%D:\" (
        set MOUNT_LETTER=%%D
        goto :FoundDrive
    )
)
:FoundDrive
if "%MOUNT_LETTER%"=="" set MOUNT_LETTER=Z

:: Clean unmount any leftover binding first
mountvol %MOUNT_LETTER%: /d >nul 2>&1

echo [*] Mounting EFI System Partition to %MOUNT_LETTER%: ...
mountvol %MOUNT_LETTER%: /s
if %errorlevel% neq 0 (
    set MOUNT_LETTER=Z
    mountvol Z: /d >nul 2>&1
    mountvol Z: /s >nul 2>&1
)

:: 4. Create directory on ESP
if not exist "%MOUNT_LETTER%:\EFI" mkdir "%MOUNT_LETTER%:\EFI"
if not exist "%MOUNT_LETTER%:\EFI\PCLock" mkdir "%MOUNT_LETTER%:\EFI\PCLock"

:: 5. Copy UEFI binary to ESP
echo [*] Copying pc_lock_preboot.efi to %MOUNT_LETTER%:\EFI\PCLock\...
copy /y "%EFI_SRC%" "%MOUNT_LETTER%:\EFI\PCLock\pc_lock_preboot.efi" >nul

:: 6. Create / Configure BCD / UEFI Firmware Boot Entry
echo [*] Registering UEFI Pre-Boot Application in Windows BCD...
for /f "tokens=2 delims={}" %%i in ('bcdedit /create /d "Cyber Cafe Pre-Boot Lock" /application BOOTAPP') do (
    set "ENTRY_GUID={%%i}"
)

if defined ENTRY_GUID (
    echo [*] Created Boot Entry: %ENTRY_GUID%
    bcdedit /set %ENTRY_GUID% device partition=%MOUNT_LETTER%: >nul
    bcdedit /set %ENTRY_GUID% path \EFI\PCLock\pc_lock_preboot.efi >nul
    bcdedit /set {fwbootmgr} displayorder %ENTRY_GUID% /addfirst >nul
    echo [SUCCESS] UEFI Pre-Boot Lock is now set as the FIRST BOOT PRIORITY!
)

:: 7. Unmount EFI Partition
mountvol %MOUNT_LETTER%: /d

echo.
echo =======================================================
echo [SUCCESS] Installation Complete!
echo Next time your PC restarts, it will launch the Cyber Cafe 
echo Pre-Boot Lock Screen BEFORE Windows starts.
echo =======================================================
pause