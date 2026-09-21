@echo off
echo ==================================================================
echo   PC SECURITY SYSTEM - HARDWARE AUDIT ^& COMPATIBILITY DASHBOARD
echo ==================================================================
echo [*] Launching DeployManager Hardware Diagnostic Dashboard...
echo [*] The Workstation Hardware Card will automatically display:
echo     - Motherboard Manufacturer, Model, and SKU
echo     - BIOS Vendor, Firmware Version, and Boot Mode (UEFI/Legacy)
echo     - Operator, Hostname, CPU Cores, and RAM
echo     - GPU / Graphics Card, Kernel Driver (.sys), and UEFI GOP Support
echo     - Network NIC, PCI Hardware ID, Driver (.sys), and UNDI/SNP ROM
echo     - Storage Controller, Miniport Driver (.sys), and Disk Partition Style (GPT/MBR)
echo     - EFI System Partition (ESP), Bootloaders (.efi), and Pre-Boot Readiness Score
echo     - 100%% Hardware Compatibility Assessment Badge
echo ==================================================================
echo.
if exist "%~dp0release_package\DeployManager.exe" (
    start "" "%~dp0release_package\DeployManager.exe"
) else (
    dotnet run --project "%~dp0DeployManager\DeployManager.csproj"
)
