namespace Nalix.Common.Package.Enums;

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
    /// Packet uses TCP.
    /// </summary>
    Reliable = 0x10,

    /// <summary>
    /// Packet uses UDP.
    /// </summary>
    Unreliable = 0x20
}
