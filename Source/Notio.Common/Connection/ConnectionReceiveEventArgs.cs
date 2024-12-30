using System;

namespace Notio.Common.Connection;

/// <summary>
/// Event args cho tin nhắn kết nối.
/// </summary>
public class ConnectionReceiveEventArgs(byte[] data) : EventArgs
{
    /// <summary>
    /// Dữ liệu nhận được từ kết nối.
    /// </summary>
    public byte[] Data { get; } = data;

    /// <summary>
    /// Thời gian nhận được tin nhắn.
    /// </summary>
    public DateTimeOffset ReceivedTimestamp { get; } = DateTimeOffset.UtcNow;
}
