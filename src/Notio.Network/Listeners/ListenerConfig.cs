using Notio.Common.Attributes;
using Notio.Shared.Configuration;
using System;
using System.Runtime.InteropServices;

namespace Notio.Network.Listeners;

/// <summary>
/// Represents network configuration settings for socket and TCP connections.
/// </summary>
public sealed class ListenerConfig : ConfigurationBinder
{
    private int _port = 5000;

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// </summary>
    public const int True = 1;

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// </summary>
    public const int False = 0;

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// Must be within the range of 1 to 65535.
    /// Standard is 5000.
    /// </summary>
    public int Port
    {
        get => _port;
        private set
        {
            if (value < 1 || value > 65535)
                throw new ArgumentOutOfRangeException(nameof(value), "Port must be between 1 and 65535.");
            else
                _port = value;
        }
    }

    /// <summary>
    /// Gets or sets whether Nagle's algorithm is disabled (low-latency communication).
    /// Standard is true.
    /// </summary>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets the buffer size for both sending and receiving data.
    /// </summary>
    /// <value>
    /// The buffer size in bytes. Default is <c>65535</c>.
    /// </value>
    public int BufferSize { get; set; } = 65535;

    /// <summary>
    /// Gets or sets a value indicating whether the socket should use the TCP Keep-Alive mechanism.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable Keep-Alive; otherwise, <c>false</c>.
    /// </value>
    public bool KeepAlive { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the socket can reuse an address already in the TIME_WAIT state.
    /// Standard is false.
    /// </summary>
    public bool ReuseAddress { get; set; } = false;

    /// <summary>
    /// Gets a value indicating whether the current operating system is Windows.
    /// </summary>
    /// <value>
    /// <c>true</c> if the application is running on Windows; otherwise, <c>false</c>.
    /// </value>
    [ConfiguredIgnore]
    public bool IsWindows { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
