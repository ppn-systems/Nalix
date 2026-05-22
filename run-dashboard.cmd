@echo off
setlocal

cd /d "%~dp0"

echo Starting Nalix Dashboard...
echo URL: http://localhost:5200
echo.

dotnet run --project example\Dashboard\Dashboard.csproj --configuration Debug --launch-profile http

echo.
echo Dashboard process exited with code %ERRORLEVEL%.
pause
