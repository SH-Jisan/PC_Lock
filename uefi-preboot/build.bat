@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Cyber Cafe PC Lock - UEFI Pre-Boot Compiler
echo =======================================================

:: Create output directory
if not exist "bin" mkdir bin

:: Check for Clang
where clang >nul 2>nul
if %errorlevel% neq 0 (
    echo [!] Searching LLVM Clang installation paths...
    if exist "D:\Soft\LLVM\bin\clang.exe" (
        set "CLANG=D:\Soft\LLVM\bin\clang.exe"
    ) else if exist "C:\Program Files\LLVM\bin\clang.exe" (
        set "CLANG=C:\Program Files\LLVM\bin\clang.exe"
    ) else if exist "C:\Program Files (x86)\LLVM\bin\clang.exe" (
        set "CLANG=C:\Program Files (x86)\LLVM\bin\clang.exe"
    ) else (
        echo [ERROR] LLVM/Clang is required to build the standalone PE32+ UEFI executable.
        echo [INFO] You can install LLVM with: winget install LLVM.LLVM
        pause
        exit /b 1
    )
) else (
    set "CLANG=clang"
)

echo [*] Using Compiler: %CLANG%
echo [*] Compiling UEFI Pre-Boot Application (pc_lock_preboot.efi)...

"%CLANG%" -target x86_64-unknown-windows ^
    -ffreestanding ^
    -fshort-wchar ^
    -mno-red-zone ^
    -nostdlib ^
    -Wl,-subsystem:efi_application ^
    -Wl,-entry:EfiMain ^
    -Iinclude ^
    -o bin\pc_lock_preboot.efi ^
    src\efi_main.c ^
    src\graphics.c ^
    src\network.c ^
    src\chainloader.c ^
    src\memory.c

if %errorlevel% equ 0 (
    echo.
    echo =======================================================
    echo [SUCCESS] UEFI Pre-Boot binary generated successfully:
    echo           --^> bin\pc_lock_preboot.efi
    echo =======================================================
) else (
    echo.
    echo [FAILED] Compilation failed. Please check error output above.
)

pause