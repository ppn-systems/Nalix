using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Common.Infrastructure.Client;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.SDK.Configuration;

/// <summary>
/// Client transport configuration loaded by the ConfigurationManager.
/// Place appropriate keys in your configuration source under the section
/// matching this class name (TransportOptions).
/// </summary>
public sealed class TransportOptions : ConfigurationLoader, ITransportOptions
{
    /// <summary>
    /// Gets the port number for the connection.
    /// </summary>
    public System.UInt16 Port { get; set; } = 57206;

    /// <summary>
    /// Gets the server address or hostname.
    /// </summary>
    public System.String Address { get; set; } = "127.0.0.1";

    // Basic connectivity

    /// <summary>
    /// Timeout for connect attempts in milliseconds. A value of 0 means no timeout.
    /// </summary>
    public System.Int32 ConnectTimeoutMillis { get; set; } = 5000;

    /// <summary>
    /// When true, automatic reconnect is enabled following an unexpected disconnect.
    /// </summary>
    public System.Boolean ReconnectEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of reconnect attempts. 0 indicates unlimited attempts.
    /// </summary>
    public System.Int32 ReconnectMaxAttempts { get; set; } = 0; // 0 = unlimited

    /// <summary>
    /// Base delay (in milliseconds) used for exponential backoff between reconnect attempts.
    /// </summary>
    public System.Int32 ReconnectBaseDelayMillis { get; set; } = 500;

    /// <summary>
    /// Maximum delay (in milliseconds) allowed between reconnect attempts.
    /// </summary>
    public System.Int32 ReconnectMaxDelayMillis { get; set; } = 30000;

    // Keep-alive / heartbeat (ms). 0 = disabled.

    /// <summary>
    /// Interval in milliseconds to send keep-alive (heartbeat) packets. 0 disables heartbeats.
    /// </summary>
    public System.Int32 KeepAliveIntervalMillis { get; set; } = 0;

    // Socket tuning

    /// <summary>
    /// Controls the TCP_NODELAY socket option. When true, Nagle's algorithm is disabled.
    /// </summary>
    public System.Boolean NoDelay { get; set; } = true;

    /// <summary>
    /// Size (in bytes) of the socket send and receive buffer.
    /// </summary>
    public System.Int32 BufferSize { get; set; } = 8192;

    // Limits

    /// <summary>
    /// Maximum allowed packet size (header + payload) in bytes.
    /// Must be large enough to contain protocol header.
    /// </summary>
    public System.Int32 MaxPacketSize { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets the encryption key used for secure communication.
    /// Default value is an empty byte array.
    /// </summary>
    [ConfiguredIgnore]
    public System.Byte[] EncryptionKey { get; set; }

    /// <summary>
    /// Gets the encryption mode for the connection.
    /// This property is ignored during configuration binding.
    /// </summary>
    public CipherSuiteType EncryptionMode { get; set; } = CipherSuiteType.CHACHA20_POLY1305;
}