namespace Notio.Network.Firewall.Metadata;

internal readonly record struct BandwidthInfo(
    long BytesSent,
    long BytesReceived,
    System.DateTime LastResetTime,
    System.DateTime LastActivityTime
);