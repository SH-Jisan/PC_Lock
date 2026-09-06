@echo off
echo ==================================================================
echo   PC SECURITY SYSTEM - HARDWARE AUDIT ^& COMPATIBILITY DASHBOARD
echo ==================================================================
echo [*] Launching DeployManager Hardware Diagnostic Dashboard...
echo [*] The Workstation Hardware Card will automatically display:
echo     - Motherboard Manufacturer, Model, and SKU
echo     - BIOS Vendor, Firmware Version, and Boot Mode (UEFI/Legacy)
echo     - Operator, Hostname, CPU Cores, and RAM
echo     - Network Hardware, MAC Address, Active IP, and Gateway
echo     - 100%% Hardware Compatibility Assessment Badge
echo ==================================================================
echo.
if exist "%~dp0release_package\DeployManager.exe" (
    start "" "%~dp0release_package\DeployManager.exe"
) else (
    dotnet run --project "%~dp0DeployManager\DeployManager.csproj"
)
