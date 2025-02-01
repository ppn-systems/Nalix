@echo off
setlocal enabledelayedexpansion

REM Chuyển đến thư mục chứa dự án
cd /d Source

REM Dọn dẹp các build cũ
echo Cleaning up old builds...
dotnet clean
if %ERRORLEVEL% neq 0 (
    echo Error during clean. Exiting...
    pause
    exit /b %ERRORLEVEL%
)

REM Xây dựng lại project
echo Building the project...
dotnet build
if %ERRORLEVEL% neq 0 (
    echo Error during build. Exiting...
    pause
    exit /b %ERRORLEVEL%
)

cd /d Notio.Database

REM Xóa migration cũ (nếu có) - chỉ nếu migration chưa được áp dụng
echo Removing old migrations...
dotnet ef migrations remove
if %ERRORLEVEL% neq 0 (
    echo Error removing migrations. Exiting...
    pause
    exit /b %ERRORLEVEL%
)

REM Thêm migration mới (thay tên migration nếu cần)
echo Adding a new migration...
dotnet ef migrations add CreateUsersTable
if %ERRORLEVEL% neq 0 (
    echo Error adding migration. Exiting...
    pause
    exit /b %ERRORLEVEL%
)

REM Cập nhật cơ sở dữ liệu với migration mới
echo Updating the database with the new migration...
dotnet ef database update
if %ERRORLEVEL% neq 0 (
    echo Error updating the database. Exiting...
    pause
    exit /b %ERRORLEVEL%
)

REM Hoàn thành
echo Migration and database update complete.
pause