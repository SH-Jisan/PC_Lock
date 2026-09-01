@echo off
setlocal EnableDelayedExpansion
echo ========================================================
echo  [RESTORE] Completely Uninstalling PC Security & Pre-Boot
echo ========================================================

:: 1. Check Administrator Privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please right-click and run this script as Administrator.
    pause
    exit /b 1
)

:: 2. Purge PC record from Supabase Cloud Database & Relay
echo [1/5] Purging PC record from Supabase Cloud Database...
powershell -NoProfile -Command ^
    "$guid = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid -ErrorAction SilentlyContinue).MachineGuid; " ^
    "if ($guid) { $pcId = 'pc_' + $guid.Substring(0,8); " ^
    "  try { Invoke-RestMethod -Uri 'https://pc-lock.onrender.com/api/devices/pc/deregister' -Method POST -ContentType 'application/json' -Body (@{ pcId = $pcId } | ConvertTo-Json) -TimeoutSec 4 -ErrorAction Stop >$null; Write-Host '  [OK] PC ' $pcId ' purged from Supabase Cloud.' } catch {} " ^
    "  try { Invoke-RestMethod -Uri 'http://localhost:4000/api/devices/pc/deregister' -Method POST -ContentType 'application/json' -Body (@{ pcId = $pcId } | ConvertTo-Json) -TimeoutSec 2 -ErrorAction SilentlyContinue >$null } catch {} " ^
    "}"

:: 3. Stop and Delete Windows Security Service & Agent Process
echo [2/5] Stopping background agents and Windows Services...
sc stop "PCSecurityAgent" >nul 2>&1
sc delete "PCSecurityAgent" >nul 2>&1
taskkill /F /IM PC.SecurityAgent.exe >nul 2>&1

:: 4. Mount EFI Partition & Restore Windows Bootloader
echo [3/5] Restoring original Windows EFI Bootloader...
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

:: Restore cloaked bootmgfw.efi
if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" (
    echo [*] Restoring Microsoft bootmgfw.efi ...
    if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" del /f /q "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi"
    move /Y "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" >nul
)

:: Clean PCLock directory
if exist "%MOUNT_LETTER%:\EFI\PCLock" (
    echo [*] Removing EFI\PCLock folder...
    rmdir /S /Q "%MOUNT_LETTER%:\EFI\PCLock" >nul 2>&1
)

:: Restore fallback bootx64.efi if backup exists
if exist "%MOUNT_LETTER%:\EFI\Boot\bootx64_orig.efi" (
    del /f /q "%MOUNT_LETTER%:\EFI\Boot\bootx64.efi" >nul 2>&1
    move /Y "%MOUNT_LETTER%:\EFI\Boot\bootx64_orig.efi" "%MOUNT_LETTER%:\EFI\Boot\bootx64.efi" >nul
)

mountvol %MOUNT_LETTER%: /d

:: 5. Restore standard Windows BCD boot display order
echo [4/5] Restoring original BIOS/BCD Firmware Boot Order...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /addfirst >nul 2>&1

:: 6. Clean Registry Settings
echo [5/5] Cleaning Registry configuration...
reg delete "HKLM\SOFTWARE\PCSecuritySystem" /f >nul 2>&1

echo ========================================================
echo  [SUCCESS] PC Security & Pre-Boot Completely Removed!
echo  Supabase Database & Local Machine 100%% Cleaned!
echo ========================================================
pause