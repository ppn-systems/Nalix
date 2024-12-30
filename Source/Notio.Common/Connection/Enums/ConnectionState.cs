namespace Notio.Common.Connection.Enums;

/// <summary>
/// Trạng thái kết nối.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Đang kết nối.
    /// </summary>
    Connecting,

    /// <summary>
    /// Đã kết nối.
    /// </summary>
    Connected,

    /// <summary>
    /// Đã xác thực.
    /// </summary>
    Authenticated,

    /// <summary>
    /// Kết nối đã bị ngắt.
    /// </summary>
    Disconnected,
}