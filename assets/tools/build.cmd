@echo off
setlocal enabledelayedexpansion

REM Define variables for directories and migration name
set "SOURCE_DIR=%~dp0Source"
set "DB_DIR=%~dp0Notio.Database"
set "MIGRATION_NAME=CreateUsersTable"

REM Optional: Allow passing a custom migration name as a parameter
if not "%~1"=="" (
    set "MIGRATION_NAME=%~1"
)

REM Function for error handling
:HandleError
echo Error: %1. Exiting...
pause
exit /b 1

REM Use pushd/popd for directory navigation so we can return easily
echo Cleaning up old builds...
pushd "%SOURCE_DIR%" || call :HandleError "Cannot change to Source directory"
dotnet clean
if errorlevel 1 (
    popd
    call :HandleError "dotnet clean failed"
)

echo Building the project...
dotnet build
if errorlevel 1 (
    popd
    call :HandleError "dotnet build failed"
)
popd

echo Removing old migrations...
pushd "%DB_DIR%" || call :HandleError "Cannot change to Notio.Database directory"
dotnet ef migrations remove
if errorlevel 1 (
    popd
    call :HandleError "Removing migrations failed"
)

echo Adding a new migration: %MIGRATION_NAME%
dotnet ef migrations add "%MIGRATION_NAME%"
if errorlevel 1 (
    popd
    call :HandleError "Adding migration failed"
)

echo Updating the database with the new migration...
dotnet ef database update
if errorlevel 1 (
    popd
    call :HandleError "Updating database failed"
)
popd

echo Migration and database update complete.
pause
