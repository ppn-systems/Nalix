@echo off
chcp 65001 >nul & rem Đặt UTF-8 để hiển thị tiếng Việt tốt hơn

:: Định nghĩa màu sắc
set COLOR_RESET=[0m
set COLOR_GREEN=[32m
set COLOR_YELLOW=[33m
set COLOR_RED=[31m
set COLOR_CYAN=[36m

echo %COLOR_CYAN%==============================================%COLOR_RESET%
echo       🔥 GitHub Auto Commit & Version Updater 🔥      
echo %COLOR_CYAN%==============================================%COLOR_RESET%
echo.

:: Xác định thư mục làm việc (lùi 2 thư mục)
cd /d "%~dp0\..\.." || (
    echo %COLOR_RED%❌ Lỗi: Không thể chuyển thư mục!%COLOR_RESET%
    pause
    exit /b 1
)

:: Kiểm tra xem có phải Git repo không
git rev-parse --is-inside-work-tree >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo %COLOR_RED%❌ Lỗi: Thư mục không phải Git repo hoặc Git chưa được cài đặt!%COLOR_RESET%
    pause
    exit /b 1
)

:: Lấy tag phiên bản mới nhất từ Git
set LAST_VERSION=
for /f "tokens=*" %%i in ('git tag --sort=-v:refname 2^>nul') do (
    set LAST_VERSION=%%i
    goto :FOUND_VERSION
)

:: Nếu không tìm thấy tag, yêu cầu nhập thủ công
:NOT_FOUND
echo %COLOR_YELLOW%⚠️  Không tìm thấy phiên bản hợp lệ!%COLOR_RESET%
set /p LAST_VERSION=🔢 Vui lòng nhập phiên bản mới (ví dụ: 1.0.0): 
goto :CHECK_VERSION

:FOUND_VERSION
:: Kiểm tra tag có đúng định dạng không (X.Y.Z)
echo %LAST_VERSION% | findstr /r "^[0-9]\+\.[0-9]\+\.[0-9]\+$" >nul
if %ERRORLEVEL% neq 0 goto :NOT_FOUND

:: Kiểm tra lại phiên bản vừa nhập hoặc lấy từ tag
:CHECK_VERSION
for /f "tokens=1,2,3 delims=." %%a in ("%LAST_VERSION%") do (
    set MAJOR=%%a
    set MINOR=%%b
    set PATCH=%%c
)

:: Kiểm tra biến có hợp lệ không
if "%MAJOR%"=="" (
    echo %COLOR_RED%❌ Lỗi: Phiên bản không hợp lệ!%COLOR_RESET%
    pause
    exit /b 1
)

:: Tăng phiên bản
set /a PATCH+=1
if %PATCH% gtr 99 (
    set /a PATCH=0
    set /a MINOR+=1
)

:: Tạo phiên bản mới
set NEW_VERSION=%MAJOR%.%MINOR%.%PATCH%

:: Hiển thị phiên bản mới và yêu cầu xác nhận
echo.
echo %COLOR_YELLOW%📌 Phiên bản mới: %NEW_VERSION%%COLOR_RESET%
echo %COLOR_CYAN%Bạn có muốn commit với phiên bản này không? (Y/N)%COLOR_RESET%
set /p CONFIRM=Nhập lựa chọn: 
if /I not "%CONFIRM%"=="Y" (
    echo %COLOR_RED%❌ Hủy bỏ commit!%COLOR_RESET%
    pause
    exit /b 0
)

:: Thực hiện commit, tag và push
echo %COLOR_GREEN%🚀 Đang commit và push lên GitHub...%COLOR_RESET%
git add .
git commit -m "Version %NEW_VERSION%"
git tag %NEW_VERSION%
git push origin HEAD --tags

:: Kiểm tra kết quả
if %ERRORLEVEL% neq 0 (
    echo %COLOR_RED%❌ Lỗi: Push lên GitHub thất bại!%COLOR_RESET%
    pause
    exit /b 1
)

echo %COLOR_GREEN%✅ Thành công! Đã cập nhật phiên bản %NEW_VERSION% trên GitHub.%COLOR_RESET%

:: Giữ cửa sổ mở
echo.
pause
