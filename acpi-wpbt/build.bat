@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   ACPI WPBT Subsystem - Compiler (Native & UEFI)
echo =======================================================

if not exist "bin" mkdir bin

:: 1. Check for Clang / MSVC compiler
where clang >nul 2>nul
if %errorlevel% equ 0 (
    set "COMPILER=clang"
) else (
    if exist "C:\Program Files\LLVM\bin\clang.exe" (
        set "COMPILER=C:\Program Files\LLVM\bin\clang.exe"
    ) else (
        echo [ERROR] LLVM/Clang compiler is required.
        echo [INFO] Install using: winget install LLVM.LLVM
        pause
        exit /b 1
    )
)

echo [*] Using Compiler: %COMPILER%

:: 2. Compile Native Windows WPBT Payload (wpbbin_agent.exe)
echo [*] Compiling Native Windows WPBT Dropper (bin\wpbbin_agent.exe)...
%COMPILER% -target x86_64-pc-windows-msvc -O2 -mwindows ^
    -o bin\wpbbin_agent.exe ^
    src\wpbbin_agent.c -luser32 -ladvapi32

if %errorlevel% equ 0 (
    echo [SUCCESS] wpbbin_agent.exe built successfully.
) else (
    echo [WARNING] Clang MSVC target failed, trying fallback build...
    gcc -O2 -mwindows -o bin\wpbbin_agent.exe src\wpbbin_agent.c -luser32 -ladvapi32 2>nul
)

:: 3. Compile Standalone UEFI WPBT Injector (wpbt_injector.efi)
echo [*] Compiling UEFI ACPI WPBT Injector (bin\wpbt_injector.efi)...
%COMPILER% -target x86_64-unknown-windows ^
    -ffreestanding ^
    -fshort-wchar ^
    -mno-red-zone ^
    -nostdlib ^
    -Wl,-subsystem:efi_application ^
    -Wl,-entry:EfiMain ^
    -I..\uefi-preboot\include ^
    -Isrc ^
    -o bin\wpbt_injector.efi ^
    src\wpbt_injector.c

if %errorlevel% equ 0 (
    echo [SUCCESS] wpbt_injector.efi built successfully.
    echo.
    echo =======================================================
    echo [SUCCESS] All WPBT components compiled in bin\
    echo           1. bin\wpbbin_agent.exe (Windows Kernel Dropper)
    echo           2. bin\wpbt_injector.efi (UEFI ACPI Table Publisher)
    echo =======================================================
) else (
    echo [FAILED] UEFI Injector compilation failed.
)

pause
