@echo off
setlocal enabledelayedexpansion

REM ============================================
REM Configuration
REM ============================================
set PROJECT_PATH=.\Nalix.Benchmark.Framework\Nalix.Benchmark.Framework.csproj
set CONFIG=Release

cd benchmarks

echo Running benchmarks...
echo.

dotnet run ^
  --project "%PROJECT_PATH%" ^
  -c %CONFIG% ^
  -- ^
  --exporters html ^
  --stopOnFirstError true ^
  --runtimes net10.0
  
echo.
echo Done.
pause