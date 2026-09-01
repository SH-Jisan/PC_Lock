@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Cyber Cafe PC Lock - UEFI Boot Entry Uninstaller
echo =======================================================

:: 1. Check Administrator Privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script MUST be run as Administrator!
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
if "%MOUNT_LETTER%"=="" set MOUNT_LETTER=Z

echo [*] Mounting EFI System Partition to %MOUNT_LETTER%: ...
mountvol %MOUNT_LETTER%: /s

if exist "%MOUNT_LETTER%:\EFI\PCLock" (
    echo [*] Removing %MOUNT_LETTER%:\EFI\PCLock directory...
    rmdir /s /q "%MOUNT_LETTER%:\EFI\PCLock" >nul 2>&1
)

mountvol %MOUNT_LETTER%: /d

echo.
echo [SUCCESS] Pre-Boot Lock files removed.
echo =======================================================
pause