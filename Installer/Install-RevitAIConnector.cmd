@echo off
setlocal
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0Install-RevitAIConnector.ps1"
if errorlevel 1 (
  echo.
  echo Installation failed. Run this file as Administrator.
  pause
  exit /b 1
)
echo.
echo Installation completed.
pause
