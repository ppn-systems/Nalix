namespace Notio.Network.Firewall.Metadata;

internal readonly record struct ConnectionInfo(
    int CurrentConnections,
    System.DateTime LastConnectionTime,
    int TotalConnectionsToday,
    System.DateTime LastCleanupTime
);