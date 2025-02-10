namespace Notio.Network.Firewall.Models;

/// <summary>
/// Represents connection-related information for monitoring and managing network traffic.
/// </summary>
internal readonly record struct ConnectionInfo(
    int CurrentConnections, System.DateTime LastConnectionTime,
    int TotalConnectionsToday, System.DateTime LastCleanupTime
);
