namespace Notio.Common.Connection;

/// <summary>
/// Represents the state of a connection.
/// </summary>
public enum ConnectionState : byte
{
    /// <summary>
    /// No connection state has been established.
    /// </summary>
    None = 0,

    /// <summary>
    /// The connection has been successfully established.
    /// </summary>
    Connected = 1,

    /// <summary>
    /// The connection has been authenticated.
    /// </summary>
    Authenticated = 2,

    /// <summary>
    /// The connection has been disconnected.
    /// </summary>
    Disconnected = 3,

    /// <summary>
    /// The connection attempt has failed.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The connection has timed out.
    /// </summary>
    Timeout = 5
}
