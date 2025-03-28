using Notio.Shared.Configuration;

using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Listeners;

/// <summary>
/// Represents network configuration settings for socket and TCP connections.
/// </summary>
public sealed class ListenerConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// Must be within the range of 1 to 65535.
    /// Standard is 5000.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether Nagle's algorithm is disabled (low-latency communication).
    /// Standard is true.
    /// </summary>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the socket can reuse an address already in the TIME_WAIT state.
    /// Standard is false.
    /// </summary>
    public bool ReuseAddress { get; set; } = false;
}
