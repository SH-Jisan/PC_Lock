@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo   Cyber Cafe PC Lock - Windows Background Service Installer
echo ========================================================

:: Check Admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Must run as Administrator!
    echo Right-click this script and select "Run as Administrator".
    pause
    exit /b 1
)

set "TARGET_DIR=C:\Program Files\PCSecuritySystem"
set "SERVICE_NAME=PCSecurityAgentService"

:: 1. Create Target Directory
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

:: 2. Locate and copy the executable
if exist "..\bin_publish\PC.SecurityAgent.exe" (
    copy /y "..\bin_publish\PC.SecurityAgent.exe" "%TARGET_DIR%\PC.SecurityAgent.exe" >nul
) else if exist "PC.SecurityAgent.exe" (
    copy /y "PC.SecurityAgent.exe" "%TARGET_DIR%\PC.SecurityAgent.exe" >nul
) else if exist "bin\Release\net8.0-windows\win-x64\publish\PC.SecurityAgent.exe" (
    copy /y "bin\Release\net8.0-windows\win-x64\publish\PC.SecurityAgent.exe" "%TARGET_DIR%\PC.SecurityAgent.exe" >nul
) else (
    echo [ERROR] PC.SecurityAgent.exe not found!
    echo Please run publish_single_file.bat first.
    pause
    exit /b 1
)

:: Copy pre-boot efi if present
if exist "..\uefi-preboot\bin\pc_lock_preboot.efi" (
    copy /y "..\uefi-preboot\bin\pc_lock_preboot.efi" "%TARGET_DIR%\pc_lock_preboot.efi" >nul
)

:: 3. Stop and Remove existing service if already installed
sc stop %SERVICE_NAME% >nul 2>&1
timeout /t 2 /nobreak >nul
sc delete %SERVICE_NAME% >nul 2>&1

:: 4. Register Native Windows Background Service (Automatic Startup)
echo [*] Installing %SERVICE_NAME% as an Automatic Windows Service...
sc create %SERVICE_NAME% binPath= "\"%TARGET_DIR%\PC.SecurityAgent.exe\"" start= auto DisplayName= "Cyber Cafe PC Lock Agent"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to register Windows Service.
    pause
    exit /b 1
)

:: 5. Set Service Failure Recovery (Restart automatically if ever terminated)
sc failure %SERVICE_NAME% reset= 0 actions= restart/2000/restart/2000/restart/2000
sc description %SERVICE_NAME% "Guarantees Cyber Cafe workstation locking, remote mobile interception, and pre-boot self-healing."

:: 6. Start the Service Immediately
echo [*] Starting %SERVICE_NAME%...
sc start %SERVICE_NAME%

echo.
echo ========================================================
echo [SUCCESS] PERMANENT INSTALLATION COMPLETE!
echo.
echo 1. The PC Security Agent is now a native Windows Service.
echo 2. It will start AUTOMATICALLY every time the PC boots up.
echo 3. You NEVER have to click or open the .exe again!
echo 4. Runs invisibly in the background with zero user UI.
echo ========================================================
pause
