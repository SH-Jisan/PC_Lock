@echo off
cd /d "%~dp0"
title PC Security Agent
color 0A
echo Starting PC Security Agent...
set DOTNET_EXE=%~dp0..\..\dotnet\dotnet.exe
if not exist "%DOTNET_EXE%" set DOTNET_EXE=dotnet
"%DOTNET_EXE%" "%~dp0bin\App\PC.SecurityAgent.dll"
pause
