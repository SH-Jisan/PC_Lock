@echo off
setlocal enabledelayedexpansion

echo ===============================================================================
echo   PC Security System - Master Zero-Dependency Package Builder
echo ===============================================================================
echo [*] This script packages the entire software into standalone executables.
echo [*] The resulting 'release_package' folder runs on ANY Windows 10/11 PC
echo     without installing .NET 8 SDK, .NET Runtime, or LLVM/Clang!
echo ===============================================================================
echo.

:: 1. Locate Dotnet SDK
where dotnet >nul 2>nul
if %errorlevel% equ 0 (
    set "DOTNET_CMD=dotnet"
) else if exist "C:\Program Files\dotnet\dotnet.exe" (
    set "DOTNET_CMD=C:\Program Files\dotnet\dotnet.exe"
) else if exist "D:\Soft\dotnet\dotnet.exe" (
    set "DOTNET_CMD=D:\Soft\dotnet\dotnet.exe"
) else (
    echo [ERROR] .NET 8 SDK was not found on this developer machine.
    echo [INFO] To compile the standalone binaries, install the SDK once using:
    echo        winget install Microsoft.DotNet.SDK.8
    echo.
    pause
    exit /b 1
)

echo [1/4] Detected .NET SDK: %DOTNET_CMD%
echo.

:: Prepare release_package directory structure
set "DIST_DIR=%~dp0release_package"
if not exist "%DIST_DIR%" mkdir "%DIST_DIR%"
if not exist "%DIST_DIR%\Agent" mkdir "%DIST_DIR%\Agent"
if not exist "%DIST_DIR%\UEFI" mkdir "%DIST_DIR%\UEFI"
if not exist "%DIST_DIR%\MobileController" mkdir "%DIST_DIR%\MobileController"

:: 2. Publish Standalone PC Security Agent (.exe)
echo [2/4] Compiling Standalone PC Security Agent (Self-Contained Single-File)...
"%DOTNET_CMD%" publish "%~dp0pc-agent\PC.SecurityAgent.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%DIST_DIR%\Agent"

if %errorlevel% neq 0 (
    echo [ERROR] Failed to compile PC.SecurityAgent.
    pause
    exit /b 1
)

:: Copy agent service management scripts
if exist "%~dp0pc-agent\install_service.bat" copy /y "%~dp0pc-agent\install_service.bat" "%DIST_DIR%\Agent\" >nul
if exist "%~dp0pc-agent\uninstall_service.bat" copy /y "%~dp0pc-agent\uninstall_service.bat" "%DIST_DIR%\Agent\" >nul

:: Also copy agent to root of dist for immediate 1-click discovery by DeployManager
copy /y "%DIST_DIR%\Agent\PC.SecurityAgent.exe" "%DIST_DIR%\PC.SecurityAgent.exe" >nul
echo [✔] Standalone PC.SecurityAgent.exe created successfully.
echo.

:: 3. Publish Standalone DeployManager (.exe)
echo [3/4] Compiling Standalone DeployManager Hub (Self-Contained Single-File)...
"%DOTNET_CMD%" publish "%~dp0DeployManager\DeployManager.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%DIST_DIR%"

if %errorlevel% neq 0 (
    echo [ERROR] Failed to compile DeployManager.
    pause
    exit /b 1
)
echo [✔] Standalone DeployManager.exe created successfully.
echo.

:: 4. Check for Optional UEFI Pre-boot Binary & Mobile Controller
echo [4/4] Finalizing Zero-Dependency Distribution Package...
if exist "%~dp0uefi-preboot\bin\pc_lock_preboot.efi" (
    copy /y "%~dp0uefi-preboot\bin\pc_lock_preboot.efi" "%DIST_DIR%\UEFI\pc_lock_preboot.efi" >nul
    copy /y "%~dp0uefi-preboot\bin\pc_lock_preboot.efi" "%DIST_DIR%\pc_lock_preboot.efi" >nul
    echo [✔] Bundled pre-compiled UEFI firmware binary (pc_lock_preboot.efi).
) else (
    echo [i] Note: No pre-compiled pc_lock_preboot.efi found.
    echo     (Not required if using Enterprise Zero-Risk mode).
)

if exist "%~dp0mobile-app\index.html" (
    copy /y "%~dp0mobile-app\index.html" "%DIST_DIR%\MobileController\index.html" >nul
    copy /y "%~dp0mobile-app\manifest.json" "%DIST_DIR%\MobileController\manifest.json" >nul
    echo [✔] Bundled Mobile Controller Web App.
)

:: Create Target PC User Guide
(
echo ===============================================================================
echo   PC SECURITY SYSTEM - CLIENT / TARGET PC RUN GUIDE
echo ===============================================================================
echo.
echo [BANGLA]
echo এই ফোল্ডারটির ভেতরের সফটওয়্যার চালানোর জন্য অন্য কোনো পিসিতে
echo .NET 8, LLVM, Clang বা কোনো সফটওয়্যার ইনস্টল করার প্রয়োজন নেই!
echo.
echo কীভাবে চালাবেন:
echo ১. এই সম্পূর্ণ 'release_package' ফোল্ডারটি পেনড্রাইভ দিয়ে টার্গেট পিসিতে নিন।
echo ২. DeployManager.exe এর ওপর রাইট ক্লিক করে "Run as administrator" সিলেক্ট করুন।
echo ৩. স্ক্রিনের "Deploy Enterprise Security" বাটনে ক্লিক করুন।
echo    ব্যস! ব্যাকগ্রাউন্ডে পিসি সিকিউরিটি ইঞ্জিন স্থায়ীভাবে ইনস্টল ও চালু হয়ে যাবে।
echo.
echo আনইনস্টল করতে চাইলে:
echo DeployManager.exe খুলে "Completely Uninstall & Restore" বাটনে ক্লিক করুন।
echo.
echo -------------------------------------------------------------------------------
echo [ENGLISH]
echo Zero dependencies required on target client machines!
echo All .NET Core libraries and runtimes are embedded inside the standalone binaries.
echo.
echo Quick Start:
echo 1. Copy this 'release_package' folder to the target Windows 10/11 workstation.
echo 2. Right-click 'DeployManager.exe' and click 'Run as administrator'.
echo 3. Click 'Deploy Enterprise Security (Zero Boot Risk)'.
echo.
echo ===============================================================================
) > "%DIST_DIR%\HOW_TO_RUN_ON_CLIENT_PC.txt"

echo.
echo ===============================================================================
echo  🎉 [ALL DONE] Zero-Dependency Release Package Ready!
echo ===============================================================================
echo  Package Location: %DIST_DIR%
echo.
echo  Contents:
echo   • DeployManager.exe     (Standalone GUI Hub - Zero .NET required)
echo   • PC.SecurityAgent.exe  (Standalone Background Guard - Zero .NET required)
echo   • Agent\                (Service scripts and standalone agent backup)
echo   • UEFI\                 (Pre-compiled EFI binary, if built)
echo   • MobileController\     (Web-based mobile control client)
echo   • HOW_TO_RUN_ON_CLIENT_PC.txt
echo.
echo  You can now copy the 'release_package' folder to ANY other PC and run directly!
echo ===============================================================================
pause
