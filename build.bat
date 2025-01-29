@echo off

REM Chuyển đến thư mục chứa dự án
cd Source
REM Dọn dẹp các build cũ
echo Cleaning up old builds...
dotnet clean

REM Xây dựng lại project
echo Building the project...
dotnet build

cd Notio.Database

REM Xóa migration cũ (nếu có) - chỉ nếu migration chưa được áp dụng
echo Removing old migrations...
dotnet ef migrations remove

REM Thêm migration mới (thay tên migration nếu cần)
echo Adding a new migration...
dotnet ef migrations add CreateUsersTable

REM Cập nhật cơ sở dữ liệu với migration mới
echo Updating the database with the new migration...
dotnet ef database update


REM Hoàn thành
echo Migration and database update complete.
pause
