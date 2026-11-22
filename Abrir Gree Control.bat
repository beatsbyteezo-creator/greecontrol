@echo off
setlocal
powershell.exe -WindowStyle Hidden -NoLogo -NoProfile -NonInteractive -Command "Get-Process -Name GreeControl -ErrorAction SilentlyContinue | Stop-Process -Force"
timeout /t 1 /nobreak >nul
set "APP=%~dp0dist-features\GreeControl.exe"
if exist "%APP%" (
    start "" "%APP%"
    exit /b 0
)
powershell.exe -WindowStyle Hidden -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0abrir.ps1"
if errorlevel 1 pause

