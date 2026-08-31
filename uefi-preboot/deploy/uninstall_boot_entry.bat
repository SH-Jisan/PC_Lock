@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Cyber Cafe PC Lock - UEFI Boot Entry Uninstaller
echo =======================================================

:: 1. Check Administrator Privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script MUST be run as Administrator!
    echo Right-click this script and select "Run as Administrator".
    pause
    exit /b 1
)

:: 2. Mount EFI Partition
echo [*] Mounting EFI System Partition to drive S:...
mountvol S: /s

:: 3. Remove files
if exist "S:\EFI\PCLock" (
    echo [*] Removing S:\EFI\PCLock directory...
    rd /s /q "S:\EFI\PCLock"
)

:: 4. Unmount EFI Partition
mountvol S: /d

echo.
echo [*] You can verify your active boot order in Windows by running: bcdedit /enum firmware
echo [SUCCESS] Pre-Boot Lock files removed. Windows will now boot directly as usual.
echo =======================================================
pause
