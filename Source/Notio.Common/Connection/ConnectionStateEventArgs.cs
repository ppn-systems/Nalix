using Notio.Common.Connection.Enums;
using System;

namespace Notio.Common.Connection;

/// <summary>
/// Event args cho trạng thái kết nối.
/// </summary>
public class ConnectionStateEventArgs(
    ConnectionState oldState,
    ConnectionState newState,
    string reason = null) : EventArgs
{
    /// <summary>
    /// Trạng thái cũ của kết nối.
    /// </summary>
    public ConnectionState OldState { get; } = oldState;

    /// <summary>
    /// Trạng thái mới của kết nối.
    /// </summary>
    public ConnectionState NewState { get; } = newState;

    /// <summary>
    /// Lý do thay đổi trạng thái kết nối.
    /// </summary>
    public string Reason { get; } = reason;
}