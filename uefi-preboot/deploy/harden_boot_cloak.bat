@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Cyber Cafe PC Lock - BIOS F12 Cloak and Default Fallback
echo =======================================================

:: 1. Check Admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Must run as Administrator!
    echo Right-click this script and select "Run as Administrator".
    pause
    exit /b 1
)

:: 2. Locate source EFI binary
set "EFI_SRC=%~dp0..\bin\pc_lock_preboot.efi"
if not exist "%EFI_SRC%" (
    if exist "%~dp0pc_lock_preboot.efi" (
        set "EFI_SRC=%~dp0pc_lock_preboot.efi"
    ) else (
        echo [ERROR] pc_lock_preboot.efi binary not found!
        echo Please run build.bat first to compile the binary.
        pause
        exit /b 1
    )
)

:: 3. Find available drive letter and ensure clean mount state
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
    :: Fallback attempt to Z:
    set MOUNT_LETTER=Z
    mountvol Z: /d >nul 2>&1
    mountvol Z: /s >nul 2>&1
)

:: 4. Ensure standard EFI folder structure exists
if not exist "%MOUNT_LETTER%:\EFI" mkdir "%MOUNT_LETTER%:\EFI"
if not exist "%MOUNT_LETTER%:\EFI\Boot" mkdir "%MOUNT_LETTER%:\EFI\Boot"
if not exist "%MOUNT_LETTER%:\EFI\PCLock" mkdir "%MOUNT_LETTER%:\EFI\PCLock"

:: 5. Cloak bootmgfw.efi to bootmgfw_hidden.efi
if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" (
    echo [*] Cloaking Windows Boot Manager (bootmgfw.efi to bootmgfw_hidden.efi)...
    if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" del /f /q "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi"
    move "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw.efi" "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" >nul
    echo [SUCCESS] Standard Windows Boot Manager is now CLOAKED from BIOS scan!
) else (
    if exist "%MOUNT_LETTER%:\EFI\Microsoft\Boot\bootmgfw_hidden.efi" (
        echo [INFO] bootmgfw.efi is already cloaked as bootmgfw_hidden.efi.
    )
)

:: 6. Implement DEFAULT UEFI FALLBACK CLOAKING (\EFI\Boot\bootx64.efi)
echo [*] Installing Default Hardware Fallback Bootloader (\EFI\Boot\bootx64.efi)...
copy /y "%EFI_SRC%" "%MOUNT_LETTER%:\EFI\Boot\bootx64.efi" >nul
copy /y "%EFI_SRC%" "%MOUNT_LETTER%:\EFI\PCLock\pc_lock_preboot.efi" >nul

:: 7. Remove standard Windows Boot Manager from Firmware Display Order
echo [*] Hardening Firmware Boot Order (Removing direct Windows Boot Manager from F12)...
bcdedit /set {fwbootmgr} displayorder {bootmgr} /remove >nul 2>&1

:: 8. Unmount EFI Partition
mountvol %MOUNT_LETTER%: /d

echo.
echo =======================================================
echo [SUCCESS] FULL HARDENING AND DEFAULT FALLBACK COMPLETE!
echo.
echo 1. BIOS F12 Boot Menu will NO LONGER SHOW 'Windows Boot Manager'.
echo 2. The real Windows Boot Manager is safely hidden at bootmgfw_hidden.efi
echo    and only chainloaded when authorized.
echo.
echo [*] To restore default Windows behavior anytime: run restore_boot_cloak.bat
echo =======================================================
pause