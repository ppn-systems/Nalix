namespace Notio.Packets.Enums;

/// <summary>
/// Packet flags indicating different states of a packet.
/// </summary>
[System.Flags]
public enum PacketFlags : byte
{
    None = 0,

    /// <summary>
    /// Đánh dấu rằng gói tin yêu cầu xác nhận (ACK - Acknowledgement).
    /// </summary>
    AckRequired = 1,

    /// <summary>
    /// Đánh dấu rằng gói tin đã được xác nhận (ACK).
    /// </summary>
    IsAcknowledged = 2,

    /// <summary>
    /// Đánh dấu rằng gói tin đã được nén.
    /// </summary>
    IsCompressed = 4,

    /// <summary>
    /// Đánh dấu rằng gói tin đã được mã hóa.
    /// </summary>
    IsEncrypted = 8,

    /// <summary>
    /// Đánh dấu rằng gói tin được gửi với độ tin cậy cao (Reliable).
    /// </summary>
    IsReliable = 16,

    /// <summary>
    /// Đánh dấu rằng gói tin đã bị phân mảnh (chia nhỏ thành các phần nhỏ hơn).
    /// </summary>
    IsFragmented = 32,

    /// <summary>
    /// Đánh dấu rằng gói tin thuộc một luồng dữ liệu liên tục (Streaming).
    /// </summary>
    IsStream = 64,
}