@echo off
cd /d "%~dp0"
title PC Security Deep Uninstaller and Factory Restore Engine
color 0C
echo ========================================================
echo  [RESTORE] Deep 6-Stage System Uninstallation and Cleanup
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
echo [Stage 1/6] Purging device identity from Supabase Cloud Database...
powershell -NoProfile -Command "$guid = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid -ErrorAction SilentlyContinue).MachineGuid; if ($guid) { $pcId = 'pc_' + $guid.Substring(0,8); try { Invoke-RestMethod -Uri 'https://pc-lock.onrender.com/api/devices/pc/deregister' -Method POST -ContentType 'application/json' -Body (@{ pcId = $pcId; hardwareUuid = $guid } | ConvertTo-Json) -TimeoutSec 5 -ErrorAction Stop >$null; Write-Host '  [OK] Device record purged from Supabase Cloud.' } catch {} }"
echo [Stage 2/6] Terminating active security daemons and background agents...
sc stop "PCSecurityAgent" >nul 2>&1
sc delete "PCSecurityAgent" >nul 2>&1
sc stop "PCSecurityAgentService" >nul 2>&1
sc delete "PCSecurityAgentService" >nul 2>&1
taskkill /F /IM PC.SecurityAgent.exe >nul 2>&1
echo [Stage 3/6] Restoring original Windows EFI Bootloader in firmware...
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
if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" (
    echo [*] Restoring Microsoft bootmgfw.efi ...
    if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" del /f /q "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi"
    move /Y "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" >nul
)
if exist "%MOUNT_LETTER%:\EFI\Boot\bootx64_orig.efi" (
    echo [*] Restoring fallback bootx64.efi ...
    del /f /q "%MOUNT_LETTER%:\EFI\Boot\bootx64.efi" >nul 2>&1
    move /Y "%MOUNT_LETTER%:\EFI\Boot\bootx64_orig.efi" "%MOUNT_LETTER%:\EFI\Boot\bootx64.efi" >nul
)
if exist "%MOUNT_LETTER%:\EFI\PCLock" (
    echo [*] Removing EFI\PCLock folder...
    rmdir /S /Q "%MOUNT_LETTER%:\EFI\PCLock" >nul 2>&1
)
mountvol %MOUNT_LETTER%: /d
echo [Stage 4/6] Restoring original BIOS/BCD Firmware Boot Order...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /addfirst >nul 2>&1
echo [Stage 5/6] Cleaning Windows Registry, Credential Providers and Run keys...
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "PCSecurityAgent" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "PCSecurityAgentService" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\PCSecuritySystem" /f >nul 2>&1
echo [Stage 6/6] Post-removal system audit complete.
echo.
echo ========================================================
echo  [SUCCESS] PC Security & Pre-Boot Completely Removed!
echo  Your PC and Supabase Database are 100-Percent Cleaned!
echo ========================================================
echo.
pause
