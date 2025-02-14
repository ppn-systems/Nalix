namespace Notio.Network.Firewall.Metadata;

/// <summary>
/// Represents connection-related information for monitoring and managing network traffic.
/// </summary>
internal readonly record struct ConnectionInfo(
    int CurrentConnections, System.DateTime LastConnectionTime,
    int TotalConnectionsToday, System.DateTime LastCleanupTime
);
