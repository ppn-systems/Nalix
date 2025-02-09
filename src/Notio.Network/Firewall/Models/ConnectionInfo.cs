namespace Notio.Network.Firewall.Models;

/// <summary>
/// Represents connection-related information for monitoring and managing network traffic.
/// </summary>
internal readonly record struct ConnectionInfo(
    /// <summary>
    /// The current number of active connections.
    /// </summary>
    int CurrentConnections,

    /// <summary>
    /// The timestamp of the most recent connection.
    /// </summary>
    System.DateTime LastConnectionTime,

    /// <summary>
    /// The total number of connections established today.
    /// </summary>
    int TotalConnectionsToday,

    /// <summary>
    /// The timestamp of the last cleanup operation performed on connection data.
    /// </summary>
    System.DateTime LastCleanupTime
);