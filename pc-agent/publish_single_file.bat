@echo off
echo ========================================================
echo   Building Single-File Standalone PC Agent (.exe)
echo ========================================================

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ..\bin_publish

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo [SUCCESS] Single-file executable generated!
    echo Location: bin_publish\PC.SecurityAgent.exe
    echo.
    echo This single .exe can run on any Windows 10/11 PC
    echo without installing any .NET runtimes!
    echo ========================================================
) else (
    echo [ERROR] Build failed. Ensure .NET 8 SDK is installed.
)
pause
