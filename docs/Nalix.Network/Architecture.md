# Kiến trúc xử lý gói tin

## Sơ đồ tổng quan

```text
+--------------------------------------+
|          PacketDispatch              |
| (Tiếp nhận, điều phối gói tin)       |
| - HandlePacket (Byte[], Memory, Span)|
| - Deserialize gói tin thành TPacket  |
+--------------------------------------+
                  |
                  v
+--------------------------------------+
|      MultiLevelQueue                 |
| (Hàng đợi ưu tiên xử lý gói tin)     |
| - Enqueue/Dequeue gói tin            |
| - Quản lý độ ưu tiên (Priority)      |
| - Xử lý gói tin hết hạn (Expiration) |
+--------------------------------------+
                  |
                  v
+--------------------------------------+
| Middleware Pipeline                  |
| (Xử lý trung gian gói tin)           |
| - Tiền xử lý (Pre-Middleware)        |
| - Hậu xử lý (Post-Middleware)        |
+--------------------------------------+
   |               |               |
   v               v               v
+--------------------------------------+   +------------------------------------+   +------------------------------------+
| PermissionMiddleware                 |   | RateLimitMiddleware                |   | PacketTransformMiddleware          |
| (Kiểm tra quyền hạn kết nối)         |   | (Giới hạn tốc độ xử lý gói tin)    |   | (Giải mã, nén gói tin nếu cần)     |
| - So sánh Permission Level           |   | - Kiểm tra RequestRate từ Endpoint |   | - Decrypt/Decompress Packet        |
+--------------------------------------+   +------------------------------------+   +------------------------------------+
                  |                                          |
                  v                                          v
+--------------------------------------+   +------------------------------------+
| PacketDispatchOptions                |   | PacketMiddlewarePipeline           |
| (Cấu hình xử lý gói tin)             |   | - Thêm Middleware vào Pipeline     |
| - Đăng ký OpCode -> Handler Mapping  |   | - Chạy Middleware theo thứ tự      |
| - Configure Middleware               |   | - Kết nối với Handler chính        |
+--------------------------------------+   +------------------------------------+
                  |
                  v
+--------------------------------------+
|             Handler                  |
| (Logic xử lý chính của gói tin)      |
| - Mapping OpCode đến Method cụ thể   |
| - Thực hiện logic nghiệp vụ          |
| - Trả về kết quả hoặc xử lý lỗi      |
+--------------------------------------+
                  |
                  v
+--------------------------------------+
|          TCP Connection              |
| (Gửi phản hồi, dữ liệu cho client)   |
| - SendAsync (trả về kết quả)         |
| - Quản lý đóng kết nối (Dispose)     |
+--------------------------------------+
```

## Giải thích chi tiết các thành phần

### **PacketDispatch**

- **Chức năng**: Tiếp nhận gói tin từ kết nối, deserialize thành đối tượng `TPacket` để xử lý.
- **Công việc chính**:
  - `HandlePacket`: Xử lý các dạng dữ liệu như `Byte[]`, `Memory<byte>`, `Span<byte>`.
  - Deserialize gói tin để chuẩn bị cho việc xử lý.

### **MultiLevelQueue**

- **Chức năng**: Quản lý hàng đợi ưu tiên, xử lý các gói tin theo độ ưu tiên cấu hình.
- **Công việc chính**:
  - `Enqueue/Dequeue`: Thêm hoặc lấy gói tin từ hàng đợi.
  - Quản lý độ ưu tiên gói tin (`Priority`).
  - Xử lý gói tin hết hạn (`Expiration`).

### **Middleware Pipeline**

- **Chức năng**: Tiền xử lý và hậu xử lý gói tin qua các lớp middleware.
- **Công việc chính**:
  - Tiền xử lý (Pre-Middleware): Xử lý trước khi gói tin đến handler chính.
  - Hậu xử lý (Post-Middleware): Xử lý sau khi gói tin được handler xử lý.

### **PermissionMiddleware**

- **Chức năng**: Kiểm tra quyền hạn của kết nối dựa trên mức Permission Level.
- **Công việc chính**:
  - So sánh mức quyền hạn của kết nối với yêu cầu của gói tin.

### **RateLimitMiddleware**

- **Chức năng**: Giới hạn tốc độ xử lý gói tin dựa trên Endpoint của kết nối.
- **Công việc chính**:
  - Kiểm tra `RequestRate` để tránh quá tải.

### **PacketTransformMiddleware**

- **Chức năng**: Biến đổi gói tin (nén, giải mã) để đảm bảo dữ liệu được xử lý chính xác.
- **Công việc chính**:
  - Giải nén (`Decompress`) hoặc giải mã (`Decrypt`) gói tin.

### **PacketDispatchOptions**

- **Chức năng**: Cấu hình các handler dựa trên OpCode, tích hợp Middleware.
- **Công việc chính**:
  - Đăng ký ánh xạ OpCode -> Handler.
  - Cấu hình pipeline xử lý.

### **Handler**

- **Chức năng**: Logic xử lý chính cho từng gói tin.
- **Công việc chính**:
  - Xử lý logic nghiệp vụ.
  - Trả về kết quả hoặc xử lý lỗi.

### **TCP Connection**

- **Chức năng**: Gửi phản hồi đến client hoặc xử lý sự cố kết nối, quản lý vòng đời kết nối.
- **Công việc chính**:
  - `SendAsync`: Gửi dữ liệu trả về.
  - Quản lý đóng kết nối (`Dispose`).
