@echo off
setlocal enabledelayedexpansion

REM ============================================
REM Configuration
REM ============================================
set PROJECT_PATH=.\Nalix.Benchmark.Shared\Nalix.Benchmark.Shared.csproj
set CONFIG=Release

cd benchmarks

echo Running benchmarks...
echo.

dotnet run ^
  --project "%PROJECT_PATH%" ^
  -c %CONFIG% 

echo.
echo Done.
pause