@echo off
setlocal enabledelayedexpansion
title Nalix Benchmark Runner

:: Ensure we are in the benchmarks directory
cd /d "%~dp0"

:main_menu
cls
echo =====================================================================
echo                     Nalix Benchmark Runner                           
echo =====================================================================
echo.
echo Please select a benchmark suite to run:
echo.
echo   [1] Codec Benchmarks      (Nalix.Codec.Benchmarks)
echo   [2] Framework Benchmarks  (Nalix.Framework.Benchmarks)
echo   [3] Network Benchmarks    (Nalix.Network.Benchmarks)
echo   [4] Runtime Benchmarks    (Nalix.Runtime.Benchmarks)
echo   [5] Run All Benchmarks    (All suites sequentially)
echo   [6] Exit
echo.
echo =====================================================================
set /p suite_choice="Select an option (1-6): "

if "%suite_choice%"=="1" (
    set "PROJECT_NAME=Nalix.Codec.Benchmarks"
    goto project_menu
)
if "%suite_choice%"=="2" (
    set "PROJECT_NAME=Nalix.Framework.Benchmarks"
    goto project_menu
)
if "%suite_choice%"=="3" (
    set "PROJECT_NAME=Nalix.Network.Benchmarks"
    goto project_menu
)
if "%suite_choice%"=="4" (
    set "PROJECT_NAME=Nalix.Runtime.Benchmarks"
    goto project_menu
)
if "%suite_choice%"=="5" (
    goto run_all
)
if "%suite_choice%"=="6" (
    goto end_script
)

echo.
echo [ERROR] Invalid choice. Please try again.
timeout /t 2 >nul
goto main_menu


:project_menu
cls
echo =====================================================================
echo  Project: %PROJECT_NAME%
echo =====================================================================
echo.
echo   [1] Run All Benchmarks in this project (Default)
echo   [2] List All Benchmarks (Flat list)
echo   [3] Run Benchmarks with Filter (e.g. *Security*, *Serializer*)
echo   [4] Run with Custom Arguments
echo   [5] Go Back
echo.
echo =====================================================================
set /p action_choice="Select an action (1-5): "

if "%action_choice%"=="1" goto run_project_all
if "%action_choice%"=="2" goto list_benchmarks
if "%action_choice%"=="3" goto run_project_filter
if "%action_choice%"=="4" goto run_project_custom
if "%action_choice%"=="5" goto main_menu

echo.
echo [ERROR] Invalid choice. Please try again.
timeout /t 2 >nul
goto project_menu


:run_project_all
cls
echo =====================================================================
echo  Running all benchmarks in %PROJECT_NAME%
echo  Command: dotnet run -c Release --project %PROJECT_NAME%
echo =====================================================================
echo.
dotnet run -c Release --project "%PROJECT_NAME%\%PROJECT_NAME%.csproj" -- --filter "*"
echo.
echo Finished execution.
pause
goto project_menu


:list_benchmarks
cls
echo =====================================================================
echo  Listing benchmarks in %PROJECT_NAME%
echo  Command: dotnet run -c Release --project %PROJECT_NAME% -- --list flat
echo =====================================================================
echo.
dotnet run -c Release --project "%PROJECT_NAME%\%PROJECT_NAME%.csproj" -- --list flat
echo.
pause
goto project_menu


:run_project_filter
cls
echo =====================================================================
echo  Run with Filter for %PROJECT_NAME%
echo =====================================================================
echo.
echo Enter search/filter pattern (e.g. *Envelope*, *Lite*, *LZ4*).
echo Case-insensitive wildcard matches are supported.
echo.
set /p filter_pat="Filter pattern: "
if "%filter_pat%"=="" (
    echo No pattern entered. Returning to project menu.
    timeout /t 2 >nul
    goto project_menu
)

cls
echo =====================================================================
echo  Running matching benchmarks in %PROJECT_NAME%
echo  Filter: %filter_pat%
echo  Command: dotnet run -c Release --project %PROJECT_NAME% -- --filter "%filter_pat%"
echo =====================================================================
echo.
dotnet run -c Release --project "%PROJECT_NAME%\%PROJECT_NAME%.csproj" -- --filter "%filter_pat%"
echo.
echo Finished execution.
pause
goto project_menu


:run_project_custom
cls
echo =====================================================================
echo  Run with Custom Arguments for %PROJECT_NAME%
echo =====================================================================
echo.
echo Enter the extra arguments to pass to BenchmarkDotNet.
echo Example: --job ShortRun --exporters json,html
echo.
set /p custom_args="Arguments: "

cls
echo =====================================================================
echo  Running custom benchmarks in %PROJECT_NAME%
echo  Arguments: %custom_args%
echo  Command: dotnet run -c Release --project %PROJECT_NAME% -- %custom_args%
echo =====================================================================
echo.
dotnet run -c Release --project "%PROJECT_NAME%\%PROJECT_NAME%.csproj" -- %custom_args%
echo.
echo Finished execution.
pause
goto project_menu


:run_all
cls
echo =====================================================================
echo  Running All Benchmark Suites (Sequential)
echo =====================================================================
echo.
echo This will execute all benchmarks across all four projects.
echo It will take a significant amount of time to complete.
echo.
set /p confirm_all="Are you sure you want to proceed? (y/N): "
if /i not "%confirm_all%"=="y" goto main_menu

echo.
echo [1/4] Running Codec Benchmarks...
dotnet run -c Release --project "Nalix.Codec.Benchmarks\Nalix.Codec.Benchmarks.csproj" -- --filter "*"
echo.
echo [2/4] Running Framework Benchmarks...
dotnet run -c Release --project "Nalix.Framework.Benchmarks\Nalix.Framework.Benchmarks.csproj" -- --filter "*"
echo.
echo [3/4] Running Network Benchmarks...
dotnet run -c Release --project "Nalix.Network.Benchmarks\Nalix.Network.Benchmarks.csproj" -- --filter "*"
echo.
echo [4/4] Running Runtime Benchmarks...
dotnet run -c Release --project "Nalix.Runtime.Benchmarks\Nalix.Runtime.Benchmarks.csproj" -- --filter "*"
echo.
echo =====================================================================
echo  All Benchmark Suites Completed!
echo =====================================================================
echo.
pause
goto main_menu


:end_script
echo.
echo Exiting Benchmark Runner.
timeout /t 1 >nul
