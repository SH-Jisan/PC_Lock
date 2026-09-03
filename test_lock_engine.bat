@echo off
echo ==================================================================
echo   PC SECURITY SYSTEM - CUSTOM LOCK ENGINE DESKTOP TEST
echo ==================================================================
echo [*] Pre-boot firmware stage is 100%% NEUTRALIZED (No EFI touch).
echo [*] Testing custom lock screen on open window / desktop...
echo.
echo [*] HOW TO UNLOCK:
echo     1. Type master PIN: 998877 or SHJ on keypad or keyboard
echo     2. Press ENTER or click OK
echo ==================================================================
echo.
if exist "%~dp0release_package\PC.SecurityAgent.exe" (
    "%~dp0release_package\PC.SecurityAgent.exe" --test-lock
) else if exist "%~dp0pc-agent\bin\Release\net8.0-windows\win-x64\PC.SecurityAgent.exe" (
    "%~dp0pc-agent\bin\Release\net8.0-windows\win-x64\PC.SecurityAgent.exe" --test-lock
) else (
    echo [INFO] Running standalone build...
    call "%~dp0build_all_standalone.bat"
    "%~dp0release_package\PC.SecurityAgent.exe" --test-lock
)
pause
