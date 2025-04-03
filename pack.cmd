@echo off
setlocal
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0pack.ps1" -Pause
exit /b %errorlevel%