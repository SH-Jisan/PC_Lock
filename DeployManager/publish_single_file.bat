@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo   Building Single-File Standalone DeployManager (.exe)
echo ========================================================

where dotnet >nul 2>nul
if %errorlevel% equ 0 (
    set "DOTNET_CMD=dotnet"
) else if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_CMD=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
) else if exist "C:\Program Files\dotnet\dotnet.exe" (
    set "DOTNET_CMD=C:\Program Files\dotnet\dotnet.exe"
) else (
    set "DOTNET_CMD=dotnet"
)

"%DOTNET_CMD%" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ..\release_package

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo [SUCCESS] Standalone DeployManager.exe generated!
    echo Location: release_package\DeployManager.exe
    echo.
    echo Runs on any Windows 10/11 PC with ZERO .NET runtime!
    echo ========================================================
) else (
    echo [ERROR] Build failed. Ensure .NET 8 SDK is installed on this developer machine.
)
pause
