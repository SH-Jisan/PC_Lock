@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo   Building Single-File Standalone PC Agent (.exe)
echo ========================================================

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ..\release_package\Agent

if %errorlevel% equ 0 (
    if not exist "..\bin_publish" mkdir "..\bin_publish"
    copy /y "..\release_package\Agent\PC.SecurityAgent.exe" "..\bin_publish\PC.SecurityAgent.exe" >nul
    echo.
    echo ========================================================
    echo [SUCCESS] Single-file standalone executable generated!
    echo Location 1: release_package\Agent\PC.SecurityAgent.exe
    echo Location 2: bin_publish\PC.SecurityAgent.exe
    echo.
    echo This single .exe runs on any Windows 10/11 PC
    echo with ZERO .NET runtime required!
    echo ========================================================
) else (
    echo [ERROR] Build failed. Ensure .NET 8 SDK is installed on this developer machine.
)
pause
