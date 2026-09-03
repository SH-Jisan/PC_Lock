@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo   Building Single-File Standalone DeployManager (.exe)
echo ========================================================

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ..\release_package

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
