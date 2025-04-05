using Notio.Shared.Configuration;

namespace Notio.Network.Configurations;

/// <summary>
/// Represents the configuration settings for caching in the network layer.
/// This class defines the limits for outgoing and incoming cache sizes.
/// </summary>
public sealed class TransportCacheConfig : ConfigurationBinder
{
    /// <summary>
    /// Gets or sets the maximum Number of outgoing cache entries.
    /// The default value is 20.
    /// </summary>
    public int Outgoing { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum Number of incoming cache entries.
    /// The default value is 40.
    /// </summary>
    public int Incoming { get; set; } = 40;
}
