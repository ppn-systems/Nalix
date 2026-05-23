@echo off
setlocal

cd /d "%~dp0"

dotnet run --project Nalix.Dashboard\Nalix.Dashboard.csproj --configuration Debug --launch-profile http

echo.
echo Dashboard process exited with code %ERRORLEVEL%.
pause
