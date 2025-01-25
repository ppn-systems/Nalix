namespace Notio.Network.Firewall.Metadata;

/// <summary>
/// Represents a data rate limit configuration, including maximum transfer rate and burst size.
/// </summary>
internal readonly record struct DataRateLimit(
    /// <summary>
    /// The allowed data transfer rate in bytes per second.
    /// </summary>
    long BytesPerSecond,

    /// <summary>
    /// The maximum burst size in bytes that can exceed the rate limit temporarily.
    /// </summary>
    int BurstSize
);