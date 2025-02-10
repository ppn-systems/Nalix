namespace Notio.Network.Firewall.Models;

/// <summary>
/// Represents bandwidth information for monitoring network activity.
/// </summary>
internal readonly record struct BandwidthInfo(
    long BytesSent, long BytesReceived,
    System.DateTime LastResetTime, System.DateTime LastActivityTime
);
