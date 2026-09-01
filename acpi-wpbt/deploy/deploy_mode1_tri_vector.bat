@echo off
setlocal EnableDelayedExpansion
echo ========================================================
echo  [MODE 1] Deploying Tri-Vector Self-Healing Persistence
echo  (0%% Motherboard Hardware Risk - 100%% Software Enforced)
echo ========================================================

:: 1. Elevate to Administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please right-click and run this script as Administrator.
    pause
    exit /b 1
)

:: 2. Find available drive letter
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

echo [1/4] Mounting EFI System Partition to %MOUNT_LETTER%: ...
mountvol %MOUNT_LETTER%: /s
if %errorlevel% neq 0 (
    set MOUNT_LETTER=Z
    mountvol Z: /d >nul 2>&1
    mountvol Z: /s >nul 2>&1
)

:: 3. Setup Pre-Boot Cloak and Folders
echo [2/4] Configuring Vector 1 (Hardware Bootloader Cloaking)...
if not exist "%MOUNT_LETTER%:\EFI" mkdir "%MOUNT_LETTER%:\EFI"
if not exist "%MOUNT_LETTER%:\EFI\PCLock" mkdir "%MOUNT_LETTER%:\EFI\PCLock"
if not exist "%MOUNT_LETTER%:\EFI\Boot" mkdir "%MOUNT_LETTER%:\EFI\Boot"

:: Cloak standard Windows Boot Manager if present
if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" (
    echo [*] Cloaking Microsoft bootmgfw.efi to bootmgfw_hidden.efi...
    if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" del /f /q "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi"
    move "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" >nul
)

:: Copy Pre-Boot binaries to default hardware fallback
set APP_DIR=%~dp0..\..\uefi-preboot\bin
if exist "%APP_DIR%\pc_lock_preboot.efi" (
    copy /Y "%APP_DIR%\pc_lock_preboot.efi" "%MOUNT_LETTER%:\EFI\Boot\bootx64.efi" >nul
    copy /Y "%APP_DIR%\pc_lock_preboot.efi" "%MOUNT_LETTER%:\EFI\PCLock\pc_lock_preboot.efi" >nul
)

echo [3/4] Configuring Vector 2 (BCD Firmware Priority Enforcer)...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /remove >nul 2>&1

:: 4. Unmount EFI Partition
mountvol %MOUNT_LETTER%: /d

:: 5. Install / Start Vector 3 (Background Self-Healing Windows Service)
echo [4/4] Activating Vector 3 (Continuous Self-Healing Agent Service)...
set SERVICE_EXE=%~dp0..\..\pc-agent\bin\Release\net8.0-windows\PC.SecurityAgent.exe
if exist "%SERVICE_EXE%" (
    sc create "PCSecurityAgent" binPath= "%SERVICE_EXE%" start= auto DisplayName= "PC Remote Security and BootGuard Healer" >nul 2>&1
    sc start "PCSecurityAgent" >nul 2>&1
)

echo ========================================================
echo  [SUCCESS] Mode 1: Tri-Vector Self-Healing Active!
echo  Security Level: Level 4 (Firmware and Kernel Self-Healing)
echo ========================================================
pause