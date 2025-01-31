namespace Notio.Network.Package.Enums;

/// <summary>
/// Packet flags indicating different states of a packet.
/// </summary>
[System.Flags]
public enum PacketFlags : byte
{
    None = 0x00,                    // Không có cờ nào

    AckRequired = 0x01,             // Gói tin yêu cầu xác nhận
    IsAcknowledged = 0x02,          // Gói tin đã được xác nhận
    IsCompressed = 0x04,            // Gói tin đã được nén
    IsEncrypted = 0x08,             // Gói tin đã được mã hóa
    IsReliable = 0x10,              // Gói tin có độ tin cậy cao
    IsFragmented = 0x20,            // Gói tin đã bị phân mảnh
    IsStream = 0x40,                // Gói tin thuộc luồng dữ liệu liên tục
    IsSigned = 0x80,                // Gói tin đã được ký
}