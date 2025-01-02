namespace Notio.Network.Firewall.Metadata;

internal readonly record struct DataRateLimit(
    long BytesPerSecond,
    int BurstSize
);