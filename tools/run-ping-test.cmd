@echo off
title Nalix Ping RTT Load Tester
setlocal
cd /d "%~dp0.."

echo ===========================================================
echo            Nalix Ping RTT Load Tester (PC to Pi)
echo ===========================================================
echo.
set /p RASPI_IP="Enter Raspberry Pi IP address (default 192.168.1.169): "
if "%RASPI_IP%"=="" set RASPI_IP=192.168.1.169

echo.
echo Building Load Tester (Direct EXE execution)...
dotnet publish tools\Nalix.LoadTester\Nalix.LoadTester.csproj -c Release -o .\LoadTesterBin > nul

echo.
echo Firing 100 connections targeting %RASPI_IP%:57206...
echo Scenario: ping
echo Duration: 30 seconds
echo.
echo Test is running for 30 seconds... 
echo You will see live progress on the console.
echo The final report will be exported to: reports\test-data\ping_client.md
echo ===========================================================
echo.

if not exist reports\test-data mkdir reports\test-data
.\LoadTesterBin\Nalix.LoadTester.exe --scenario ping --host %RASPI_IP% --port 57206 --connections 2000 --duration 30 --warmup 2 --output reports\test-data\ping_client.md

echo.
echo ===========================================================
echo Test finished! Check reports\test-data\ping_client.md for the detailed result.
pause
