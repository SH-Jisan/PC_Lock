@echo off
setlocal
echo ========================================================
echo  [MODE 2] Building SPI Flash ROM Hardware Injection Kit
echo ========================================================

set OUTPUT_DIR=%~dp0output
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo [1/3] Copying ACPI Source Language (ASL) Table...
copy /Y "%~dp0..\asl\wpbt.asl" "%OUTPUT_DIR%\wpbt.asl" >nul

echo [2/3] Packing Native UEFI DXE Driver Sources and Definitions...
copy /Y "%~dp0wpbt_dxe.c" "%OUTPUT_DIR%\wpbt_dxe.c" >nul
copy /Y "%~dp0wpbt_dxe.inf" "%OUTPUT_DIR%\wpbt_dxe.inf" >nul

echo [3/3] Generating Hardware Flashing Documentation...
copy /Y "%~dp0FLASH_INSTRUCTIONS.md" "%OUTPUT_DIR%\FLASH_INSTRUCTIONS.md" >nul

echo ========================================================
echo  Mode 2 Hardware Injection Package Ready at: %OUTPUT_DIR%
echo ========================================================
pause