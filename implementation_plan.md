# Refactor ObjectPoolManager Initialization (Lazy Resolution)

Mục tiêu: Giải quyết vấn đề vòng lặp phụ thuộc (circular dependency) trong quá trình khởi tạo giữa `ObjectPoolManager` và `TaskManager`. Hiện tại `ObjectPoolManager` khởi tạo job dọn dẹp (`obj.trim`) ngay trong constructor bằng cách lấy `TaskManager` qua `InstanceManager`. Điều này ép hệ thống phải tạo `TaskManager` quá sớm trước khi nó có thể được cấu hình đầy đủ.

## Proposed Changes

### `Nalix.Framework`

#### [MODIFY] [ObjectPoolManager.cs](file:///e:/Cs/nalix/src/Nalix.Framework/Memory/Objects/ObjectPoolManager.cs)

**1. Sửa Constructor:**
- Xóa bỏ việc gọi `InstanceManager.Instance.GetOrCreateInstance<TaskManager>()` trong Constructor.
- Bỏ từ khóa `readonly` của biến `_trimJob` (vì sẽ được gán sau).
- Khởi tạo một biến cờ `private int _trimJobScheduled;` để lock-free check.

**2. Thêm hàm `EnsureTrimJobScheduled()`:**
- Hàm này sẽ kiểm tra cờ `_trimJobScheduled` bằng `Interlocked.CompareExchange`.
- Nếu thắng lock, và `_config.EnableObjectTrimming` là `true`, tiến hành lấy `TaskManager` (via `GetOrCreateInstance`) và đăng ký `ScheduleRecurring`.
- Lưu trữ kết quả vào biến `_trimJob` để tiện quản lý/pause/resume sau này.

**3. Gọi hàm vào Fast Path:**
- Chèn hàm `EnsureTrimJobScheduled()` vào điểm chạm đầu tiên khi Framework bắt đầu có tải, cụ thể là bên trong phương thức `Get<T>()`.
- Có thể kết hợp kiểm tra `if (_trimJobScheduled == 0)` trước khi gọi hàm để đảm bảo Fast Path của `Get<T>()` vẫn đạt hiệu suất cực cao (không bị chậm bởi Method Call hay Interlocked).

## Verification Plan

### Automated Tests
- Chạy các Unit Test liên quan tới `ObjectPoolManagerTests` để đảm bảo không gãy đổ logic cũ.
- Viết/Chạy thêm Test đảm bảo Trim Job vẫn được khởi tạo (tạo mock TaskManager, gọi 1 lần `Get<T>()` và verify `ScheduleRecurring` được gọi đúng 1 lần).

### User Feedback Required
- Vì bạn yêu cầu "chỉ plan không code", tôi đã lên tài liệu này. Bạn có thể kiểm tra cấu trúc Plan xem đã đúng hướng chưa. Nếu đồng ý, tôi sẽ đánh dấu là hoàn tất phiên "grill" này.
