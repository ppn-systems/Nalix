using Notio.Shared.Configuration;

using System.ComponentModel.DataAnnotations;

namespace Notio.Network;

/// <summary>
/// Represents network configuration settings for socket and TCP connections.
/// </summary>
public sealed class NetworkConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// Must be within the range of 1 to 65535.
    /// Default is 5000.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the buffer size for receiving data.
    /// The value must be at least 64KB.
    /// Default is 64KB (65536 bytes).
    /// </summary>
    [Range(1024, int.MaxValue)]
    public int ReceiveBufferSize { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the buffer size for sending data.
    /// The value must be at least 64KB.
    /// Default is 64KB (65536 bytes).
    /// </summary>
    [Range(1024, int.MaxValue)]
    public int SendBufferSize { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the linger timeout in seconds.
    /// Default is 30 seconds.
    /// </summary>
    public int LingerTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the timeout for receiving data in milliseconds.
    /// Default is 5000 milliseconds.
    /// </summary>
    public int ReceiveTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the timeout for sending data in milliseconds.
    /// Default is 5000 milliseconds.
    /// </summary>
    public int SendTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether TCP KeepAlive is enabled.
    /// Default is true.
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Nagle's algorithm is disabled (low-latency communication).
    /// Default is true.
    /// </summary>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the socket can reuse an address already in the TIME_WAIT state.
    /// Default is false.
    /// </summary>
    public bool ReuseAddress { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the socket supports both IPv4 and IPv6.
    /// Default is false.
    /// </summary>
    public bool DualMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout for accepting a connection in milliseconds.
    /// Default is 10000 milliseconds (10 seconds).
    /// </summary>
    public int AcceptConnectionTimeoutMilliseconds { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the low watermark for receiving data in bytes.
    /// Default is 1MB (1024 * 1024 bytes).
    /// </summary>
    public int SocketReceiveLowWatermark { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the low watermark for sending data in bytes.
    /// Default is 1MB (1024 * 1024 bytes).
    /// </summary>
    public int SocketSendLowWatermark { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets whether the socket is in blocking mode.
    /// Default is true.
    /// </summary>
    public bool IsBlocking { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether the port is valid (within the range 1 to 65535).
    /// This property is not configured and is only for validation purposes.
    /// </summary>
    [ConfiguredIgnore]
    public bool IsValidPort => Port is >= 1 and <= 65535;
}
