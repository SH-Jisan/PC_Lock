@echo off
setlocal
echo =======================================================
echo  Building Universal Pre-Boot Micro-Core (Wired + Wi-Fi)
echo =======================================================

set OUTPUT_DIR=%~dp0..\bin\micro-core
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo [1/3] Assembling Micro-Core InitRD and Kernel config...
copy /Y "%~dp0init.sh" "%OUTPUT_DIR%\init.sh" >nul
copy /Y "%~dp0preboot_guard.py" "%OUTPUT_DIR%\preboot_guard.py" >nul

echo [2/3] Writing Universal Wi-Fi & Ethernet Network configuration template...
(
  echo {
  echo   "ssid": "AUTO_SYNCED_FROM_WINDOWS",
  echo   "psk": "AUTO_SYNCED_FROM_WINDOWS",
  echo   "relay_url": "https://pc-lock-relay.onrender.com"
  echo }
) > "%OUTPUT_DIR%\wifi_config.json"

echo [3/3] Pre-Boot Micro-Core Bundle Ready at: %OUTPUT_DIR%
echo =======================================================
echo  Pre-Boot Micro-Core Packaging Complete!
echo =======================================================
