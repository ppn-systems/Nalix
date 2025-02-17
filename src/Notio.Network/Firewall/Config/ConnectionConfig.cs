using Notio.Shared.Configuration;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall.Config;

/// <summary>
/// Configuration for limiting the number of concurrent connections per IP address.
/// This configuration helps manage and control the number of simultaneous connections from each IP.
/// </summary>
public sealed class ConnectionConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets the maximum number of connections allowed per IP address.
    /// </summary>
    /// <remarks>
    /// This value is limited to a range of 1 to 1000, where 100 is the default value.
    /// The configuration defines how many connections a single IP address can maintain simultaneously.
    /// </remarks>
    [Range(1, 1000)]
    public int MaxConnectionsPerIpAddress { get; set; } = 100;
}

/// <summary>
/// Represents different levels of connection limits that can be applied for managing simultaneous connections.
/// </summary>
public enum ConnectionLimit
{
    /// <summary>
    /// Represents a low number of simultaneous connections, typically for minimal traffic environments.
    /// </summary>
    Low,

    /// <summary>
    /// Represents a medium number of simultaneous connections, suitable for moderate traffic environments.
    /// </summary>
    Medium,

    /// <summary>
    /// Represents a high number of simultaneous connections, suitable for high-traffic environments.
    /// </summary>
    High,

    /// <summary>
    /// Represents unlimited simultaneous connections, with no restrictions on the number of connections.
    /// </summary>
    Unlimited
}
