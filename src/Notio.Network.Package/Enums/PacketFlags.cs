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
    /// IPacket requires acknowledgment.
    /// </summary>
    AckRequired = 0x01,             // Gói tin yêu cầu xác nhận

    /// <summary>
    /// IPacket has been acknowledged.
    /// </summary>
    IsAcknowledged = 0x02,          // Gói tin đã được xác nhận

    /// <summary>
    /// IPacket is compressed.
    /// </summary>
    IsCompressed = 0x04,            // Gói tin đã được nén

    /// <summary>
    /// IPacket is encrypted.
    /// </summary>
    IsEncrypted = 0x08,             // Gói tin đã được mã hóa

    /// <summary>
    /// IPacket is reliable (guaranteed delivery).
    /// </summary>
    IsReliable = 0x10,              // Gói tin có độ tin cậy cao

    /// <summary>
    /// IPacket is fragmented.
    /// </summary>
    IsFragmented = 0x20,            // Gói tin đã bị phân mảnh

    /// <summary>
    /// IPacket belongs to a continuous data stream.
    /// </summary>
    IsStream = 0x40,                // Gói tin thuộc luồng dữ liệu liên tục

    /// <summary>
    /// IPacket is signed.
    /// </summary>
    IsSigned = 0x80,                // Gói tin đã được ký
}