@echo off
chcp 65001 >nul  & rem Đặt bảng mã UTF-8 để hiển thị màu sắc tốt hơn

:: Định nghĩa màu sắc
set COLOR_RESET=[0m
set COLOR_GREEN=[32m
set COLOR_YELLOW=[33m
set COLOR_RED=[31m
set COLOR_CYAN=[36m

:: Hiển thị banner đẹp
echo %COLOR_CYAN%==============================================
echo        🔥 NuGet Package Uploader 🔥       
echo ==============================================%COLOR_RESET%

:: Nhập API Key từ người dùng
set /p API_KEY=%COLOR_YELLOW%Enter your NuGet API Key: %COLOR_RESET%

:: Kiểm tra API Key có rỗng không
if "%API_KEY%"=="" (
    echo %COLOR_RED%❌ Error: API Key cannot be empty!%COLOR_RESET%
    exit /b 1
)

:: Kiểm tra xem có file .nupkg không
for %%F in (..\build\bin\Release\*.nupkg) do (
    set FOUND_PACKAGE=1
    goto :UPLOAD
)

:: Nếu không tìm thấy file nào, báo lỗi
echo %COLOR_RED%❌ Error: No .nupkg files found in Release folder!%COLOR_RESET%
exit /b 1

:UPLOAD
echo %COLOR_GREEN%🚀 Pushing NuGet package(s)...%COLOR_RESET%

dotnet nuget push ..\build\bin\Release\*.nupkg --api-key %API_KEY% --source https://api.nuget.org/v3/index.json --skip-duplicate

:: Kiểm tra kết quả của lệnh
if %ERRORLEVEL% neq 0 (
    echo %COLOR_RED%❌ Upload failed! Please check your API key or internet connection.%COLOR_RESET%
    exit /b %ERRORLEVEL%
) else (
    echo %COLOR_GREEN%✅ Upload successful! Package(s) are now available on NuGet.%COLOR_RESET%
)

pause