@echo off
setlocal enabledelayedexpansion
title "PC Security System - Autonomous Post-Install Verification and Health Engine"
cls
echo ===============================================================================
echo   PC SECURITY SYSTEM - AUTONOMOUS POST-INSTALLATION HEALTH CHECK
echo ===============================================================================
echo [*] Inspecting system integrity, background agent, startup keys, and cloud...
echo.

set FAILURES=0

:: Check 1: Permanent Files
echo [1/5] Checking Permanent Storage (C:\Program Files\PCSecuritySystem)...
if exist "C:\Program Files\PCSecuritySystem\PC.SecurityAgent.exe" (
    echo     [PASS] PC.SecurityAgent.exe verified intact.
) else (
    echo     [FAIL] PC.SecurityAgent.exe is MISSING in C:\Program Files\PCSecuritySystem
    echo            ^> CAUSE: File was not copied or was quarantined by Windows Defender.
    echo            ^> FIX  : Add C:\Program Files\PCSecuritySystem to Antivirus exclusions.
    set /a FAILURES+=1
)

:: Check 2: Background Process
echo.
echo [2/5] Checking Background Daemon Process Liveness...
powershell -NoProfile -Command "Get-Process PC.SecurityAgent -ErrorAction SilentlyContinue | Select-Object -First 1 Id, WorkingSet64" > "%temp%\pclock_proc.txt" 2>&1
findstr /i "Id" "%temp%\pclock_proc.txt" >nul 2>&1
if %errorlevel% equ 0 (
    powershell -NoProfile -Command "$p = Get-Process PC.SecurityAgent -ErrorAction SilentlyContinue | Select-Object -First 1; Write-Host ('    [PASS] PC.SecurityAgent is LIVE in memory (PID: ' + $p.Id + ', RAM: ' + [math]::Round($p.WorkingSet64/1MB, 1) + ' MB)')"
) else (
    echo     [FAIL] PC.SecurityAgent process is NOT running in Windows memory
    echo            ^> CAUSE: The background security daemon crashed or failed to start.
    echo            ^> FIX  : Run "C:\Program Files\PCSecuritySystem\PC.SecurityAgent.exe" manually to inspect errors.
    set /a FAILURES+=1
)
del "%temp%\pclock_proc.txt" 2>nul

:: Check 3: Startup Run Key
echo.
echo [3/5] Checking Windows Auto-Start Persistence (Registry Run Key)...
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "PCSecurityAgent" >nul 2>&1
if %errorlevel% equ 0 (
    echo     [PASS] HKLM Startup Key verified. Software will auto-start on reboot.
) else (
    reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "PCSecurityAgent" >nul 2>&1
    if !errorlevel! equ 0 (
        echo     [PASS] HKCU Startup Key verified. Software will auto-start on reboot.
    ) else (
        echo     [FAIL] No startup autorun key found in registry
        echo            ^> CAUSE: Administrator privileges were missing during setup.
        echo            ^> FIX  : Re-run DeployManager as Administrator.
        set /a FAILURES+=1
    )
)

:: Check 4: Cloud Gateway
echo.
echo [4/5] Checking Cloud Gateway Relay Reachability (https://pc-lock.onrender.com)...
powershell -NoProfile -Command "try { $r = Invoke-WebRequest -Uri 'https://pc-lock.onrender.com' -TimeoutSec 5 -UseBasicParsing; if ($r.StatusCode -lt 500) { Write-Host ('    [PASS] Connected to Cloud Relay (HTTP ' + $r.StatusCode + ' OK)') } else { Write-Host ('    [FAIL] Cloud server error: ' + $r.StatusCode); exit 1 } } catch { Write-Host ('    [FAIL] Cannot reach Cloud Server: ' + $_.Exception.Message); exit 1 }"
if %errorlevel% neq 0 (
    echo            ^> CAUSE: PC is offline or Firewall is blocking outbound HTTPS requests.
    echo            ^> FIX  : Check internet connection and allow outbound port 443 in Firewall.
    set /a FAILURES+=1
)

:: Check 5: Hardware Profile
echo.
echo [5/5] Checking Hardware-Adaptive Profile Cache...
if exist "C:\Program Files\PCSecuritySystem\hardware_audit.json" (
    echo     [PASS] Machine hardware profile cached for auto-adaptive recovery.
) else (
    echo     [NOTICE] hardware_audit.json not found in installation folder.
)

echo.
echo ===============================================================================
if %FAILURES% equ 0 (
    echo  [ALL CHECKS PASSED] System is 100 percent Operational, Protected, and Online!
    echo ===============================================================================
    echo  The PC Security System is running properly and actively safeguarding this PC.
) else (
    echo  [DIAGNOSTIC ALERT] Detected !FAILURES! issue^(s^) during verification!
    echo ===============================================================================
    echo  Please review the causes and fixes listed above to resolve the problems.
)
echo ===============================================================================
echo.
pause
