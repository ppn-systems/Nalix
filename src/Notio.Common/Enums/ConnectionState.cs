namespace Notio.Common.Enums;

/// <summary>
/// Represents the state of a connection.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// The connection has been successfully established.
    /// </summary>
    Connected,

    /// <summary>
    /// The connection has been authenticated.
    /// </summary>
    Authenticated,

    /// <summary>
    /// The connection has been disconnected.
    /// </summary>
    Disconnected,
}