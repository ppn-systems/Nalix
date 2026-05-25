@echo off
cd /d "%~dp0.."
title Pull Nalix Metrics from Pi
setlocal

set "RASPI_IP=192.168.1.169"
set "RASPI_USER=nalix"

echo ===========================================================
echo   Pulling backend.log and metrics.csv from Raspberry Pi
echo ===========================================================
echo.
set /p USER_INPUT="Please enter the Raspberry Pi IP address [Default: %RASPI_IP%]: "
if not "%USER_INPUT%"=="" set "RASPI_IP=%USER_INPUT%"

set /p USER_NAME_INPUT="Please enter the SSH username [Default: %RASPI_USER%]: "
if not "%USER_NAME_INPUT%"=="" set "RASPI_USER=%USER_NAME_INPUT%"

echo.
echo Pulling files from %RASPI_USER%@%RASPI_IP%:~/ ...
echo.

if not exist reports\test-data mkdir reports\test-data
scp %RASPI_USER%@%RASPI_IP%:~/nalix-backend/backend.log .\reports\test-data\backend.log
scp %RASPI_USER%@%RASPI_IP%:~/nalix-backend/metrics.csv .\reports\test-data\metrics.csv
scp %RASPI_USER%@%RASPI_IP%:~/nalix-backend/sar_cpu.log .\reports\test-data\sar_cpu.log

echo.
echo ===========================================================
if exist ".\reports\test-data\metrics.csv" (
    echo [SUCCESS] Files have been downloaded to reports\test-data folder!
    echo - backend.log
    echo - metrics.csv
) else (
    echo [ERROR] Failed to download files. Did the test run on the Pi?
)
echo ===========================================================
pause
