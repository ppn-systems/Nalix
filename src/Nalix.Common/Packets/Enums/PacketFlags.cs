// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Packets.Enums;

/// <summary>
/// Defines bitwise flags that describe the state or properties of a network packet.
/// </summary>
/// <remarks>
/// This enumeration is marked with <see cref="System.FlagsAttribute"/> so that multiple flags can be combined using a bitwise OR.
/// </remarks>
[System.Flags]
public enum PacketFlags : System.Byte
{
    /// <summary>
    /// No flags are set.
    /// The packet is uncompressed, unencrypted, and not fragmented.
    /// Typically used for simple, default, or test packets.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// The packet payload has been compressed to reduce its size.
    /// Should be decompressed on the receiving side before processing.
    /// </summary>
    Compressed = 0x02,

    /// <summary>
    /// The packet payload has been encrypted for secure transmission.
    /// Should be decrypted with the correct key on the receiving side.
    /// </summary>
    Encrypted = 0x04,

    /// <summary>
    /// The packet is a fragment of a larger message that exceeded the maximum allowed size.
    /// The receiving side should reassemble fragments in the correct order.
    /// </summary>
    Fragmented = 0x08,

    /// <summary>
    /// The packet is sent over a reliable transport protocol (typically TCP).
    /// Guarantees delivery and ordering without the need for manual retries.
    /// </summary>
    Reliable = 0x10,

    /// <summary>
    /// The packet is sent over an unreliable transport protocol (typically UDP).
    /// Best suited for real-time data where occasional loss is acceptable.
    /// </summary>
    Unreliable = 0x20,

    /// <summary>
    /// The packet has been acknowledged by the receiver.
    /// Often used to stop retransmissions or mark successful delivery.
    /// </summary>
    Acknowledged = 0x40,

    /// <summary>
    /// The packet is a system-level message that does not contain user data.
    /// Examples include ping, heartbeat, handshake, or system error notifications.
    /// </summary>
    System = 0x80

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
