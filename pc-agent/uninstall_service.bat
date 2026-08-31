@echo off
echo ========================================================
echo   Cyber Cafe PC Lock - Uninstall Windows Service
echo ========================================================

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Must run as Administrator!
    pause
    exit /b 1
)

set "SERVICE_NAME=PCSecurityAgentService"

echo [*] Stopping %SERVICE_NAME%...
sc stop %SERVICE_NAME% >nul 2>&1
timeout /t 2 /nobreak >nul

echo [*] Removing %SERVICE_NAME% from Windows Services...
sc delete %SERVICE_NAME%

echo.
echo ========================================================
echo [SUCCESS] Windows Background Service uninstalled.
echo ========================================================
pause
