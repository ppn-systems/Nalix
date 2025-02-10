using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall.Configuration;

/// <summary>
/// Configuration for limiting the number of concurrent connections per IP address.
/// </summary>
public sealed class ConnectionConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets the maximum number of connections allowed per IP address.
    /// </summary>
    /// <remarks>
    /// This value is limited to a range of 1 to 1000, where 100 is the default.
    /// </remarks>
    [Range(1, 1000)]
    public int MaxConnectionsPerIpAddress { get; set; } = 100;
}
