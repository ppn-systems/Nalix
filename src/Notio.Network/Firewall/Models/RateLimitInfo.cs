namespace Notio.Network.Firewall.Models;

/// <summary>
/// Represents a data rate limit configuration, including maximum transfer rate and burst size.
/// </summary>
internal readonly record struct RateLimitInfo(
    long BytesPerSecond, int BurstSize
);
