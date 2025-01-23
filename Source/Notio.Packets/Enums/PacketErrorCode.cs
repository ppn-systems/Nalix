namespace Notio.Packets.Enums;

/// <summary>
/// Enum quản lý các mã lỗi liên quan đến xử lý Packet.
/// </summary>
public enum PacketErrorCode : int
{
    /// <summary>
    /// Không có lỗi.
    /// </summary>
    None = 0,

    /// <summary>
    /// Payload rỗng.
    /// </summary>
    EmptyPayload = 1,

    /// <summary>
    /// Payload đã được mã hóa.
    /// </summary>
    AlreadyEncrypted = 2,

    /// <summary>
    /// Payload chưa được mã hóa.
    /// </summary>
    NotEncrypted = 3,

    /// <summary>
    /// Payload đã được ký.
    /// </summary>
    AlreadySigned = 4,

    /// <summary>
    /// Payload chưa được ký.
    /// </summary>
    NotSigned = 5,

    /// <summary>
    /// Khóa không hợp lệ (không phải 256-bit hoặc null).
    /// </summary>
    InvalidKey = 6,

    /// <summary>
    /// Lỗi mã hóa Payload.
    /// </summary>
    EncryptionFailed = 7,

    /// <summary>
    /// Lỗi giải mã Payload.
    /// </summary>
    DecryptionFailed = 8,

    /// <summary>
    /// Header không hợp lệ.
    /// </summary>
    InvalidHeader = 9,

    /// <summary>
    /// Lỗi không xác định.
    /// </summary>
    UnknownError = 10,
}