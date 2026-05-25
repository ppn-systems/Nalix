@echo off
title Nalix Core - 2-Hour High-Load Soak Test
cls
echo =====================================================================
echo             Nalix Core High-Performance Soak Test
echo =====================================================================
echo.
echo This script will execute a long-running payload soak test using
echo the compiled Release version of Nalix.LoadTester.
echo.
echo Test Configurations:
echo   - Scenario     : payload
echo   - Target Host  : 127.0.0.1 (Localhost)
echo   - Target Port  : 57206 (Backend Default Listen Port)
echo   - Concurrency  : 500 Concurrent Clients
echo   - Duration     : 2 Hours (7,200 Seconds)
echo   - Timeout      : 5,000 ms
echo   - Payload Size : 1,500 bytes
echo.
echo ---------------------------------------------------------------------
echo WARNING: This is a heavy stress test designed to run for 2 hours.
echo Please ensure the Backend Server is running before launching.
echo ---------------------------------------------------------------------
echo.

set /p confirm="Are you ready to launch the 2-Hour Soak Test? (y/N): "
if /i not "%confirm%"=="y" (
    echo.
    echo Soak test execution cancelled by user.
    timeout /t 3 >nul
    exit /b 0
)

echo.
echo Ensuring Nalix.LoadTester is compiled in Release mode...
dotnet build "%~dp0Nalix.LoadTester.csproj" -c Release >nul

echo.
echo Launching Nalix.LoadTester...
echo Running command: .\bin\Release\net10.0\Nalix.LoadTester.exe --scenario payload --host 127.0.0.1 --port 57206 --connections 500 --duration 7200 --timeout 5000 --payload-size 1500
echo.

"%~dp0bin\Release\net10.0\Nalix.LoadTester.exe" --scenario payload --host 127.0.0.1 --port 57206 --connections 500 --duration 7200 --timeout 5000 --payload-size 1500

echo.
echo =====================================================================
echo Soak Test Completed!
echo =====================================================================
echo.
pause
