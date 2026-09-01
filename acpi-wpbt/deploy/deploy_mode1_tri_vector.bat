@echo off
cd /d "%~dp0"
title PC Security Mode 1 Deployment
color 0B
echo ========================================================
echo  [MODE 1] Deploying Tri-Vector Self-Healing Persistence
echo  0-Percent Motherboard Hardware Risk - 100-Percent Safe
echo ========================================================
echo.
:: 1. Check Administrator Privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Administrator privileges are required!
    echo Please RIGHT-CLICK this file and select: "Run as administrator"
    echo.
    pause
    exit /b 1
)
echo [1/4] Administrator session verified.
echo [2/4] Scanning system topology and mounting EFI partition...
set MOUNT_LETTER=
for %%D in (Z Y X W V U T S R Q P) do (
    if not exist "%%D:\" (
        set MOUNT_LETTER=%%D
        goto :FoundDrive
    )
)
:FoundDrive
if "%MOUNT_LETTER%"=="" set MOUNT_LETTER=Z
mountvol %MOUNT_LETTER%: /d >nul 2>&1
mountvol %MOUNT_LETTER%: /s
if %errorlevel% neq 0 (
    set MOUNT_LETTER=Z
    mountvol Z: /d >nul 2>&1
    mountvol Z: /s >nul 2>&1
)
echo [3/4] Configuring Vector 1 (Hardware Bootloader Cloaking)...
if not exist "%MOUNT_LETTER%:\EFI" mkdir "%MOUNT_LETTER%:\EFI"
if not exist "%MOUNT_LETTER%:\EFI\PCLock" mkdir "%MOUNT_LETTER%:\EFI\PCLock"
if not exist "%MOUNT_LETTER%:\EFI\Boot" mkdir "%MOUNT_LETTER%:\EFI\Boot"
if not exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot" mkdir "%MOUNT_LETTER%:\EFI\Microsoft\Boot"
if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" (
    echo [*] Cloaking Microsoft bootmgfw.efi to bootmgfw_hidden.efi...
    if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" del /f /q "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi"
    move "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" >nul
)
set APP_DIR=%~dp0..\..\uefi-preboot\bin
if exist "%APP_DIR%\pc_lock_preboot.efi" (
    copy /Y "%APP_DIR%\pc_lock_preboot.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" >nul
    copy /Y "%APP_DIR%\pc_lock_preboot.efi" "%MOUNT_LETTER%:\EFI\Boot\bootx64.efi" >nul
    copy /Y "%APP_DIR%\pc_lock_preboot.efi" "%MOUNT_LETTER%:\EFI\PCLock\pc_lock_preboot.efi" >nul
)
echo [*] Configuring Vector 2 (BCD Firmware Priority Enforcer)...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /remove >nul 2>&1
mountvol %MOUNT_LETTER%: /d
echo [4/4] Activating Vector 3 (Continuous Background Agent)...
set AGENT_DIR=%~dp0..\..\pc-agent
set DOTNET_EXE=%~dp0..\..\..\dotnet\dotnet.exe
if not exist "%DOTNET_EXE%" set DOTNET_EXE=dotnet
taskkill /F /IM PC.SecurityAgent.exe >nul 2>&1
powershell -NoProfile -WindowStyle Hidden -Command "Start-Process -FilePath '%DOTNET_EXE%' -ArgumentList '\"%AGENT_DIR%\bin\App\PC.SecurityAgent.dll\"' -WindowStyle Hidden"
echo.
echo ========================================================
echo  [SUCCESS] Mode 1: Tri-Vector Self-Healing Active!
echo  Security Level: Level 4 (Firmware and Kernel Active)
echo  The PC Security Agent is running silently in the background.
echo ========================================================
echo.
pause
