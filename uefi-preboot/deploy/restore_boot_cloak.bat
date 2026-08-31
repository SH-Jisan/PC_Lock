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

:: 1. Mount EFI Partition
echo [*] Mounting EFI System Partition to drive S:...
mountvol S: /s
if %errorlevel% neq 0 (
    echo [ERROR] Failed to mount EFI partition.
    pause
    exit /b 1
)

:: 2. Un-cloak bootmgfw_hidden.efi -> bootmgfw.efi
if exist "S:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" (
    echo [*] Restoring bootmgfw_hidden.efi -^> bootmgfw.efi...
    ren "S:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" "bootmgfw.efi"
    echo [SUCCESS] Standard Windows Boot Manager restored.
)

:: 3. Restore default bootx64.efi fallback if backup exists
if exist "S:\EFI\Boot\bootx64_orig.efi" (
    echo [*] Restoring original bootx64.efi...
    del "S:\EFI\Boot\bootx64.efi" 2>nul
    ren "S:\EFI\Boot\bootx64_orig.efi" "bootx64.efi"
    echo [SUCCESS] Original default EFI fallback restored.
)

:: 4. Restore {bootmgr} in BCD display order
echo [*] Restoring {bootmgr} in Firmware Boot Order...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /addfirst 2>nul

:: 5. Unmount EFI Partition
mountvol S: /d

echo.
echo =======================================================
echo [SUCCESS] Windows Boot Manager and Default Fallback restored to normal.
echo =======================================================
pause
