using Notio.Shared.Configuration;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall;

/// <summary>
/// Configuration for limiting the number of concurrent connections per IP address.
/// This configuration helps manage and control the number of simultaneous connections from each IP.
/// </summary>
public sealed class ConnectionConfig : ConfiguredBinder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionConfig"/> class with a specified connection limit.
    /// </summary>
    /// <param name="limit">The predefined connection limit to apply.</param>
    public ConnectionConfig(ConnectionLimit limit)
    {
        switch (limit)
        {
            case ConnectionLimit.Low:
                MaxConnectionsPerIpAddress = 20;
                break;

            case ConnectionLimit.Medium:
                MaxConnectionsPerIpAddress = 100;
                break;

            case ConnectionLimit.High:
                MaxConnectionsPerIpAddress = 500;
                break;

            case ConnectionLimit.Unlimited:
                MaxConnectionsPerIpAddress = 10000;
                break;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionConfig"/> class with a default connection limit of <see cref="ConnectionLimit.Medium"/>.
    /// </summary>
    public ConnectionConfig()
        : this(ConnectionLimit.Medium)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// <c>true</c> if logging is enabled; otherwise, <c>false</c>.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// <c>true</c> if metrics collection is enabled; otherwise, <c>false</c>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

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
