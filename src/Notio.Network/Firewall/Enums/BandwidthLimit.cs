namespace Notio.Network.Firewall.Enums;

/// <summary>
/// Specifies the bandwidth limits for different application use cases.
/// </summary>
public enum BandwidthLimit
{
    /// <summary>
    /// Low bandwidth limit, typically used for small applications with minimal network usage.
    /// </summary>
    Low,

    /// <summary>
    /// Medium bandwidth limit, typically suitable for normal applications.
    /// </summary>
    Medium,

    /// <summary>
    /// High bandwidth limit, suitable for large applications with high network usage.
    /// </summary>
    High,

    /// <summary>
    /// Unlimited bandwidth limit, which imposes no restrictions (use with caution).
    /// </summary>
    Unlimited
}
