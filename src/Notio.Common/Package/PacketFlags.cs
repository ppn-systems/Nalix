namespace Notio.Common.Package;

/// <summary>
/// IPacket flags indicating different states of a packet.
/// </summary>
[System.Flags]
public enum PacketFlags : byte
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
    /// Packet is signed for integrity verification.
    /// </summary>
    Signed = 0x10,                // Gói tin đã được ký để kiểm tra tính toàn vẹn

    /// <summary>
    /// Packet is a control packet (used for protocol commands).
    /// </summary>
    Control = 0x20,               // Gói tin điều khiển (dùng cho lệnh giao thức)

    /// <summary>
    /// Packet is acknowledged (used in reliable transmission).
    /// </summary>
    Acknowledged = 0x40,          // Gói tin đã được xác nhận (ACK)

    /// <summary>
    /// Packet is retransmitted (used for error correction).
    /// </summary>
    Retransmitted = 0x80          // Gói tin được gửi lại do lỗi
}
