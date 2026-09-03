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

:: 1. Locate Dotnet SDK with actual SDK installed
set "DOTNET_CMD="
if exist "D:\Soft\dotnet\dotnet.exe" (
    set "DOTNET_CMD=D:\Soft\dotnet\dotnet.exe"
) else (
    for /f "delims=" %%i in ('where dotnet 2^>nul') do (
        "%%i" --list-sdks 2>nul | findstr /r "[0-9]" >nul
        if not errorlevel 1 (
            if not defined DOTNET_CMD set "DOTNET_CMD=%%i"
        )
    )
)
if not defined DOTNET_CMD (
    if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "DOTNET_CMD=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
)
if not defined DOTNET_CMD (
    if exist "C:\Program Files\dotnet\dotnet.exe" set "DOTNET_CMD=C:\Program Files\dotnet\dotnet.exe"
)
if not defined DOTNET_CMD set "DOTNET_CMD=dotnet"

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
    if not defined CI pause
    exit /b 1
)

:: Copy agent service management scripts
if exist "%~dp0pc-agent\install_service.bat" copy /y "%~dp0pc-agent\install_service.bat" "%DIST_DIR%\Agent\" >nul
if exist "%~dp0pc-agent\uninstall_service.bat" copy /y "%~dp0pc-agent\uninstall_service.bat" "%DIST_DIR%\Agent\" >nul

:: Also copy agent to root of dist for immediate 1-click discovery by DeployManager
copy /y "%DIST_DIR%\Agent\PC.SecurityAgent.exe" "%DIST_DIR%\PC.SecurityAgent.exe" >nul
echo [OK] Standalone PC.SecurityAgent.exe created successfully.
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
    if not defined CI pause
    exit /b 1
)
echo [OK] Standalone DeployManager.exe created successfully.
echo.

:: 4. Check for Optional UEFI Pre-boot Binary & Mobile Controller
echo [4/4] Finalizing Zero-Dependency Distribution Package...
if exist "%~dp0uefi-preboot\bin\pc_lock_preboot.efi" (
    copy /y "%~dp0uefi-preboot\bin\pc_lock_preboot.efi" "%DIST_DIR%\UEFI\pc_lock_preboot.efi" >nul
    copy /y "%~dp0uefi-preboot\bin\pc_lock_preboot.efi" "%DIST_DIR%\pc_lock_preboot.efi" >nul
    echo [OK] Bundled pre-compiled UEFI firmware binary: pc_lock_preboot.efi
) else (
    echo [i] Note: No pre-compiled pc_lock_preboot.efi found.
    echo     Not required if using Enterprise Zero-Risk mode.
)

if exist "%~dp0mobile-app\index.html" (
    copy /y "%~dp0mobile-app\index.html" "%DIST_DIR%\MobileController\index.html" >nul
    copy /y "%~dp0mobile-app\manifest.json" "%DIST_DIR%\MobileController\manifest.json" >nul
    echo [OK] Bundled Mobile Controller Web App.
)

:: Create Target PC User Guide
set "GUIDE_FILE=%DIST_DIR%\HOW_TO_RUN_ON_CLIENT_PC.txt"
echo =============================================================================== > "%GUIDE_FILE%"
echo   PC SECURITY SYSTEM - CLIENT / TARGET PC RUN GUIDE >> "%GUIDE_FILE%"
echo =============================================================================== >> "%GUIDE_FILE%"
echo( >> "%GUIDE_FILE%"
echo [BANGLA] >> "%GUIDE_FILE%"
echo Ei folder er shob kisu chalate kono .NET ba Clang lagbe na! >> "%GUIDE_FILE%"
echo( >> "%GUIDE_FILE%"
echo Kivabe chalaben: >> "%GUIDE_FILE%"
echo 1. Ei release_package folder-ti pendrive diye target PC-te nin. >> "%GUIDE_FILE%"
echo 2. DeployManager.exe-te Right-click kore "Run as administrator" din. >> "%GUIDE_FILE%"
echo 3. "Deploy Enterprise Security (Zero Boot Risk)" batone click korun. >> "%GUIDE_FILE%"
echo( >> "%GUIDE_FILE%"
echo Uninstall korte chaile: >> "%GUIDE_FILE%"
echo DeployManager.exe theke "Completely Uninstall & Restore" batone click korun. >> "%GUIDE_FILE%"
echo( >> "%GUIDE_FILE%"
echo ------------------------------------------------------------------------------- >> "%GUIDE_FILE%"
echo [ENGLISH] >> "%GUIDE_FILE%"
echo Zero dependencies required on target client machines! >> "%GUIDE_FILE%"
echo All .NET Core libraries and runtimes are embedded inside the standalone binaries. >> "%GUIDE_FILE%"
echo( >> "%GUIDE_FILE%"
echo Quick Start: >> "%GUIDE_FILE%"
echo 1. Copy this release_package folder to the target Windows 10/11 workstation. >> "%GUIDE_FILE%"
echo 2. Right-click DeployManager.exe and click Run as administrator. >> "%GUIDE_FILE%"
echo 3. Click Deploy Enterprise Security (Zero Boot Risk). >> "%GUIDE_FILE%"
echo( >> "%GUIDE_FILE%"
echo =============================================================================== >> "%GUIDE_FILE%"

echo.
echo ===============================================================================
echo  [ALL DONE] Zero-Dependency Release Package Ready!
echo ===============================================================================
echo  Package Location: %DIST_DIR%
echo.
echo  Contents:
echo   - DeployManager.exe     (Standalone GUI Hub - Zero .NET required)
echo   - PC.SecurityAgent.exe  (Standalone Background Guard - Zero .NET required)
echo   - Agent\                (Service scripts and standalone agent backup)
echo   - UEFI\                 (Pre-compiled EFI binary, if built)
echo   - MobileController\     (Web-based mobile control client)
echo   - HOW_TO_RUN_ON_CLIENT_PC.txt
echo.
echo  You can now copy the release_package folder to ANY other PC and run directly!
echo ===============================================================================
if not defined CI pause
