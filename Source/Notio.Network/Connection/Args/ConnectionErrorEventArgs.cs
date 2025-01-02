using Notio.Common.INetwork.Args;
using Notio.Common.INetwork.Enums;
using System;

namespace Notio.Network.Connection.Args;

/// <summary>
/// Event args cho các lỗi kết nối.
/// </summary>
public class ConnectionErrorEventArgs(ConnectionError errorType, string message)
    : EventArgs, IErrorEventArgs
{
    /// <summary>
    /// Loại lỗi.
    /// </summary>
    public ConnectionError ErrorType { get; } = errorType;

    /// <summary>
    /// Chi tiết lỗi.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Thời gian lỗi xảy ra.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}