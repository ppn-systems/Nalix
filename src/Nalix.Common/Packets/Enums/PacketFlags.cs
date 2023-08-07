namespace Nalix.Common.Packets.Enums;

/// <summary>
/// IPacket flags indicating different states of a packet.
/// </summary>
[System.Flags]
public enum PacketFlags : System.Byte
{
    /// <summary>
    /// No flags set for the packet.
    /// </summary>
    None = 0x00,                    // Không có cờ nào

    /// <summary>
    /// Packet is compressed.
    /// </summary>
    Compressed = 0x02,            // Gói tin đã được nén

    /// <summary>
    /// Packet is encrypted.
    /// </summary>
    Encrypted = 0x04,             // Gói tin đã được mã hóa

    /// <summary>
    /// Packet is fragmented.
    /// </summary>
    Fragmented = 0x08,            // Gói tin đã bị phân mảnh

    /// <summary>
    /// Packet uses TCP.
    /// </summary>
    Reliable = 0x10,

    /// <summary>
    /// Packet uses UDP.
    /// </summary>
    Unreliable = 0x20,

    /// <summary>
    /// Packet has been acknowledged by receiver.
    /// </summary>
    Acknowledged = 0x40,

    /// <summary>
    /// Packet is a system-level message (e.g. ping, heartbeat).
    /// </summary>
    System = 0x80,

    // -----------------------------
    // Ghi chú sử dụng cho PacketFlags
    // -----------------------------

    // None
    // Không có cờ nào được bật.
    // Dùng khi gói tin là đơn giản nhất, không mã hóa, không nén, không phân mảnh.
    // Thường dùng cho các gói dữ liệu mặc định hoặc test.

    // Compressed
    // Gói tin đã được nén để giảm kích thước truyền tải.
    // Thường dùng khi payload lớn và cần tiết kiệm băng thông.
    // Cần giải nén ở phía nhận trước khi xử lý.

    // Encrypted
    // Gói tin đã được mã hóa để đảm bảo an toàn khi truyền qua mạng.
    // Dùng cho dữ liệu nhạy cảm như tài khoản, mật khẩu, token xác thực, v.v.
    // Phía nhận phải giải mã đúng key hoặc sẽ bị lỗi giải mã.

    // Fragmented
    // Gói tin đã bị phân mảnh vì vượt quá giới hạn kích thước cho phép.
    // Dùng cho những payload lớn (như file, hình ảnh, bản đồ, v.v.)
    // Cần ghép lại ở phía nhận theo đúng thứ tự.

    // Reliable
    // Gói tin được gửi qua giao thức đáng tin cậy (thường là TCP).
    // Dùng cho dữ liệu yêu cầu đúng thứ tự, không được mất mát, như đăng nhập, giao dịch.
    // Không cần retry thủ công vì giao thức đảm bảo điều đó.

    // Unreliable
    // Gói tin được gửi qua giao thức không đảm bảo (thường là UDP).
    // Dùng cho dữ liệu thời gian thực như vị trí, trạng thái nhân vật trong game,
    // nơi việc mất 1 vài gói tin không quan trọng bằng tốc độ truyền nhanh.
    // Có thể dùng kèm timestamp nếu cần kiểm tra mới/cũ.

    // Acknowledged
    // Gói tin này đã được xác nhận là đã nhận bởi phía bên kia.
    // Dùng để đánh dấu là không cần gửi lại nữa hoặc cập nhật trạng thái thành công.
    // Có thể dùng trong cơ chế tự định nghĩa Reliable-UDP.

    // System
    // Gói tin cấp hệ thống, không mang dữ liệu người dùng.
    // Dùng cho ping, heartbeat, handshake, thông báo lỗi hệ thống.
    // Phân biệt với gói ứng dụng thông thường.
}
