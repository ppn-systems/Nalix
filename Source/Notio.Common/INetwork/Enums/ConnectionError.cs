namespace Notio.Common.INetwork.Enums;

/// <summary>
/// Các loại lỗi có thể xảy ra khi kết nối.
/// </summary>
public enum ConnectionError
{
    /// <summary>
    /// Lỗi không xác định.
    /// </summary>
    Undefined,

    /// <summary>
    /// Lỗi khi đọc dữ liệu.
    /// </summary>
    ReadError,

    /// <summary>
    /// Lỗi khi gửi dữ liệu.
    /// </summary>
    SendError,

    /// <summary>
    /// Lỗi kết nối mạng.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Lỗi mã hóa.
    /// </summary>
    EncryptionError,

    DecryptionError,

    /// <summary>
    /// Dữ liệu không khớp.
    /// </summary>
    DataMismatch,

    /// <summary>
    /// Dữ liệu quá lớn.
    /// </summary>
    DataTooLarge,

    /// <summary>
    /// Kết nối bị ngắt.
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// Lỗi xác thực.
    /// </summary>
    AuthenticationError,

    /// <summary>
    /// Lỗi khi đóng kết nối.
    /// </summary>
    CloseError,

    StreamClosed
}