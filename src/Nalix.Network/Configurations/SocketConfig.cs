using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;
using System;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents network configuration settings for socket and Reliable connections.
/// </summary>
public sealed class SocketConfig : ConfigurationLoader
{
    #region Fields

    private int _tcpPort = 52000;
    private int _udpPort = 52001;

    #endregion Fields

    #region Constants

    /// <summary>
    /// Constant value for True (1).
    /// </summary>
    public const int True = 1;

    /// <summary>
    /// Constant value for False (0).
    /// </summary>
    public const int False = 0;

    #endregion Constants

    #region Properties

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// Must be within the range of 1 to 65535.
    /// Standard is 5000.
    /// </summary>
    public int TcpPort
    {
        get => _tcpPort;
        private set
        {
            if (value < 1 || value > 65535)
                throw new System.ArgumentOutOfRangeException(
                    nameof(value), "TcpPort must be between 1 and 65535.");
            else
                _tcpPort = value;
        }
    }

    /// <summary>
    /// Gets or sets the UDP port number.
    /// Must be within the range of 1 to 65535.
    /// Default is 5001.
    /// </summary>
    public int UdpPort
    {
        get => _udpPort;
        private set
        {
            if (value is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(value), "UDP port must be between 1 and 65535.");
            _udpPort = value;
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
    /// Gets or sets a value indicating whether the socket should use the Reliable Keep-Alive mechanism.
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
    public bool IsWindows { get; set; } = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform
        (System.Runtime.InteropServices.OSPlatform.Windows);

    #endregion Properties
}
