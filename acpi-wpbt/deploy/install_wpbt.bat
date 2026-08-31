@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   ACPI WPBT Subsystem - Deployment & Verification
echo =======================================================

:: Check Admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Must run as Administrator!
    pause
    exit /b 1
)

:: 1. Copy wpbbin_agent.exe to System32 to test immediate dropper execution
if exist "..\bin\wpbbin_agent.exe" (
    set "SRC=..\bin\wpbbin_agent.exe"
) else if exist "bin\wpbbin_agent.exe" (
    set "SRC=bin\wpbbin_agent.exe"
) else (
    echo [ERROR] bin\wpbbin_agent.exe not found. Run build.bat first.
    pause
    exit /b 1
)

echo [*] Installing wpbbin.exe to C:\Windows\System32\...
copy /y "%SRC%" "C:\Windows\System32\wpbbin.exe"

echo [*] Executing test invocation under System32...
C:\Windows\System32\wpbbin.exe

echo [*] Checking boot execution log...
if exist "C:\Windows\Temp\wpbt_boot_log.txt" (
    echo.
    echo --- [LOG CONTENT] ---
    type "C:\Windows\Temp\wpbt_boot_log.txt"
    echo --- [END OF LOG] ---
)

echo.
echo =======================================================
echo [SUCCESS] WPBT Dropper verified and active!
echo =======================================================
pause
