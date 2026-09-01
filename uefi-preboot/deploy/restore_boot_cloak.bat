@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Cyber Cafe PC Lock - Restore Windows Boot Manager
echo =======================================================

:: Check Admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Must run as Administrator!
    pause
    exit /b 1
)

:: Find available drive letter
set MOUNT_LETTER=
for %%D in (Z Y X W V U T S R Q P) do (
    if not exist "%%D:\" (
        set MOUNT_LETTER=%%D
        goto :FoundDrive
    )
)
:FoundDrive
if "%MOUNT_LETTER%"=="" set MOUNT_LETTER=Z

echo [*] Mounting EFI System Partition to %MOUNT_LETTER%: ...
mountvol %MOUNT_LETTER%: /s
if not exist "%MOUNT_LETTER%:\EFI" (
    echo [ERROR] Failed to mount EFI partition.
    pause
    exit /b 1
)

:: 2. Un-cloak bootmgfw_hidden.efi to bootmgfw.efi
if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" (
    echo [*] Restoring bootmgfw_hidden.efi to bootmgfw.efi...
    move /Y "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" >nul
    echo [SUCCESS] Standard Windows Boot Manager restored.
)

:: 3. Restore {bootmgr} in BCD display order
echo [*] Restoring {bootmgr} in Firmware Boot Order...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /addfirst >nul 2>&1

:: 4. Unmount EFI Partition
mountvol %MOUNT_LETTER%: /d

echo.
echo =======================================================
echo [SUCCESS] Windows Boot Manager restored to normal.
echo =======================================================
pause