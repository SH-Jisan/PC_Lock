@echo off
title PC Remote Security Agent
cd /d "%~dp0"
if exist "..\dotnet\dotnet.exe" (
    "..\dotnet\dotnet.exe" "bin\Release\net8.0-windows\PC.SecurityAgent.dll"
) else (
    dotnet "bin\Release\net8.0-windows\PC.SecurityAgent.dll"
)