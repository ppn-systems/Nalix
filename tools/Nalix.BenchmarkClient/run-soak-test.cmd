@echo off
title Nalix Core — 2-Hour High-Load Soak Test
cls
echo =====================================================================
echo           Nalix Core High-Performance Soak Testing Script            
echo =====================================================================
echo.
echo This script will execute a long-running soak test (endurance test)
echo using the compiled Release version of the Nalix BenchmarkClient.
echo.
echo Test Configurations:
echo   - Target Host  : 127.0.0.1 (Localhost)
echo   - Target Port  : 57206 (Backend Default Listen Port)
echo   - Concurrency  : 500 Concurrent Clients
echo   - Duration     : 2 Hours (7,200 Seconds)
echo.
echo ---------------------------------------------------------------------
echo WARNING: This is a heavy stress test designed to run for 2 hours.
echo Please ensure the Backend Server is running in Extreme JIT Mode
echo (with DOTNET_TC_QuickJit=0 and DOTNET_TieredPGO=1) before launching.
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
echo Ensuring the BenchmarkClient is compiled in Release mode...
dotnet build "%~dp0Nalix.BenchmarkClient.csproj" -c Release >nul

echo.
echo Launching Nalix BenchmarkClient Native Executable...
echo Running command: .\bin\Release\net10.0\Nalix.BenchmarkClient.exe 127.0.0.1 57206 500 7200
echo.

"%~dp0bin\Release\net10.0\Nalix.BenchmarkClient.exe" 127.0.0.1 57206 500 7200

echo.
echo =====================================================================
echo Soak Test Completed!
echo =====================================================================
echo.
pause
