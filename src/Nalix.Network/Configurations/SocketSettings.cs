using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents network configuration settings for socket and Reliable connections.
/// </summary>
public sealed class SocketSettings : ConfigurationLoader
{
    #region Fields

    private System.UInt16 _port = 57206;

    #endregion Fields

    #region Constants

    /// <summary>
    /// Constant value for True (1).
    /// </summary>
    public const System.Int32 True = 1;

    /// <summary>
    /// Constant value for False (0).
    /// </summary>
    public const System.Int32 False = 0;

    #endregion Constants

    #region Properties

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// Must be within the range of 1 to 65535.
    /// Standard is 57206.
    /// </summary>
    public System.UInt16 Port
    {
        get => this._port;
        set
        {
            if (value is <= System.UInt16.MinValue or > System.UInt16.MaxValue)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Port must be between 1 and 65535.");
            }

            this._port = value;
        }
    }

    /// <summary>
    /// Indicates whether to use IPv4 or IPv6.
    /// </summary>
    public System.Boolean EnableIPv6 { get; set; } = false;

    /// <summary>
    /// Gets or sets whether Nagle's algorithm is disabled (low-latency communication).
    /// Standard is true.
    /// </summary>
    public System.Boolean NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of parallel connections.
    /// </summary>
    public System.Int32 MaxParallel { get; set; } = 5;

    /// <summary>
    /// Gets or sets the buffer size for both sending and receiving data.
    /// </summary>
    /// <value>
    /// The buffer size in bytes. Default is <c>65535</c>.
    /// </value>
    public System.Int32 BufferSize { get; set; } = 65535;

    /// <summary>
    /// Gets or sets the maximum size of the UDP packet.
    /// </summary>
    public System.Int32 MinUdpSize { get; set; } = 32;

    /// <summary>
    /// Gets or sets a value indicating whether the socket should use the Reliable Keep-Alive mechanism.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable Keep-Alive; otherwise, <c>false</c>.
    /// </value>
    public System.Boolean KeepAlive { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the socket can reuse an address already in the TIME_WAIT state.
    /// Standard is false.
    /// </summary>
    public System.Boolean ReuseAddress { get; set; } = false;


    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed by the socket.
    /// Default is 1000.
    /// </summary>
    public System.Int32 MaxConcurrentConnections { get; set; } = 1000;

    /// <summary>
    /// Gets a value indicating whether the current operating system is Windows.
    /// </summary>
    /// <value>
    /// <c>true</c> if the application is running on Windows; otherwise, <c>false</c>.
    /// </value>
    [ConfiguredIgnore]
    public System.Boolean IsWindows { get; set; } = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform
        (System.Runtime.InteropServices.OSPlatform.Windows);

    #endregion Properties
}