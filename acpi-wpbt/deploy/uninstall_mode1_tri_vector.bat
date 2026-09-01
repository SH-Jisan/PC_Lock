@echo off
setlocal EnableDelayedExpansion
echo ========================================================
echo  [RESTORE] Uninstalling Mode 1 Tri-Vector Pre-Boot Cloak
echo ========================================================

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please run as Administrator.
    pause
    exit /b 1
)

set MOUNT_LETTER=
for %%D in (Z Y X W V U T S R Q P) do (
    if not exist "%%D:\" (
        set MOUNT_LETTER=%%D
        goto :FoundDrive
    )
)
:FoundDrive
if "%MOUNT_LETTER%"=="" (
    echo [ERROR] No available drive letters found to mount EFI partition.
    exit /b 1
)

mountvol %MOUNT_LETTER%: /s
if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" (
    echo [*] Restoring Microsoft bootmgfw.efi ...
    move /Y "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" >nul
)

if exist "%MOUNT_LETTER%:\EFI\PCLock" (
    rmdir /S /Q "%MOUNT_LETTER%:\EFI\PCLock" >nul 2>&1
)

mountvol %MOUNT_LETTER%: /d

echo [*] Restoring standard BCD boot display order...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /addfirst >nul 2>&1

echo ========================================================
echo  [SUCCESS] Pre-Boot Cloak Uninstalled and Restored!
echo ========================================================
pause