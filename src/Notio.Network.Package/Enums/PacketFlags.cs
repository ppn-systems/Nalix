namespace Notio.Network.Package.Enums;

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
    /// Packet requires acknowledgment.
    /// </summary>
    AckRequired = 0x01,             // Gói tin yêu cầu xác nhận

    /// <summary>
    /// Packet has been acknowledged.
    /// </summary>
    IsAcknowledged = 0x02,          // Gói tin đã được xác nhận

    /// <summary>
    /// Packet is compressed.
    /// </summary>
    IsCompressed = 0x04,            // Gói tin đã được nén

    /// <summary>
    /// Packet is encrypted.
    /// </summary>
    IsEncrypted = 0x08,             // Gói tin đã được mã hóa

    /// <summary>
    /// Packet is reliable (guaranteed delivery).
    /// </summary>
    IsReliable = 0x10,              // Gói tin có độ tin cậy cao

    /// <summary>
    /// Packet is fragmented.
    /// </summary>
    IsFragmented = 0x20,            // Gói tin đã bị phân mảnh

    /// <summary>
    /// Packet belongs to a continuous data stream.
    /// </summary>
    IsStream = 0x40,                // Gói tin thuộc luồng dữ liệu liên tục

    /// <summary>
    /// Packet is signed.
    /// </summary>
    IsSigned = 0x80,                // Gói tin đã được ký
}
