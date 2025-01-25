namespace Notio.Network.Firewall.Metadata;

/// <summary>
/// Represents bandwidth information for monitoring network activity.
/// </summary>
internal readonly record struct BandwidthInfo(
    /// <summary>
    /// The total number of bytes sent.
    /// </summary>
    long BytesSent,

    /// <summary>
    /// The total number of bytes received.
    /// </summary>
    long BytesReceived,

    /// <summary>
    /// The timestamp of the last reset of bandwidth statistics.
    /// </summary>
    System.DateTime LastResetTime,

    /// <summary>
    /// The timestamp of the last activity recorded.
    /// </summary>
    System.DateTime LastActivityTime
);