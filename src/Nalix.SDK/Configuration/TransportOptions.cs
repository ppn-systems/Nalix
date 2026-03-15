// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Transport;
using Nalix.Common.Security.Enums;
using Nalix.Common.Shared.Attributes;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.SDK.Configuration;

/// <summary>
/// Client transport configuration loaded by the ConfigurationManager.
/// Place appropriate keys in your configuration source under the section
/// matching this class name (TransportOptions).
/// </summary>
[IniComment("Client transport configuration — controls connectivity, reconnect policy, socket tuning, and encryption")]
public sealed class TransportOptions : ConfigurationLoader, ITransportOptions
{
    /// <summary>
    /// Gets the port number for the connection.
    /// </summary>
    [IniComment("Server port to connect to (1–65535)")]
    public System.UInt16 Port { get; set; } = 57206;

    /// <summary>
    /// Gets the server address or hostname.
    /// </summary>
    [IniComment("Server IP address or hostname")]
    public System.String Address { get; set; } = "127.0.0.1";

    // Basic connectivity

    /// <summary>
    /// Timeout for connect attempts in milliseconds. A value of 0 means no timeout.
    /// </summary>
    [IniComment("Connect attempt timeout in milliseconds (0 = no timeout)")]
    public System.Int32 ConnectTimeoutMillis { get; set; } = 5000;

    /// <summary>
    /// When true, automatic reconnect is enabled following an unexpected disconnect.
    /// </summary>
    [IniComment("Automatically reconnect after an unexpected disconnect")]
    public System.Boolean ReconnectEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of reconnect attempts. 0 indicates unlimited attempts.
    /// </summary>
    [IniComment("Max reconnect attempts (0 = unlimited)")]
    public System.Int32 ReconnectMaxAttempts { get; set; } = 0;

    /// <summary>
    /// Base delay (in milliseconds) used for exponential backoff between reconnect attempts.
    /// </summary>
    [IniComment("Base delay in milliseconds for exponential backoff between reconnect attempts")]
    public System.Int32 ReconnectBaseDelayMillis { get; set; } = 500;

    /// <summary>
    /// Maximum delay (in milliseconds) allowed between reconnect attempts.
    /// </summary>
    [IniComment("Maximum delay in milliseconds between reconnect attempts")]
    public System.Int32 ReconnectMaxDelayMillis { get; set; } = 30000;

    // Keep-alive / heartbeat (ms). 0 = disabled.

    /// <summary>
    /// Interval in milliseconds to send keep-alive (heartbeat) packets. 0 disables heartbeats.
    /// </summary>
    [IniComment("Heartbeat interval in milliseconds (0 = disabled)")]
    public System.Int32 KeepAliveIntervalMillis { get; set; } = 20_000;

    // Socket tuning

    /// <summary>
    /// Controls the TCP_NODELAY socket option. When true, Nagle's algorithm is disabled.
    /// </summary>
    [IniComment("Disable Nagle's algorithm for lower latency (recommended: true)")]
    public System.Boolean NoDelay { get; set; } = true;

    /// <summary>
    /// Size (in bytes) of the socket send and receive buffer.
    /// </summary>
    [IniComment("Socket send and receive buffer size in bytes")]
    public System.Int32 BufferSize { get; set; } = 8192;

    // Limits

    /// <summary>
    /// Maximum allowed packet size (header + payload) in bytes.
    /// </summary>
    [IniComment("Maximum packet size in bytes including header and payload")]
    public System.Int32 MaxPacketSize { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets the encryption key used for secure communication.
    /// </summary>
    [ConfiguredIgnore]
    public System.Byte[] EncryptionKey { get; set; }

    /// <summary>
    /// Gets the encryption mode for the connection.
    /// </summary>
    [IniComment("Cipher suite used for packet encryption (e.g., CHACHA20, SALSA20, CHACHA20_POLY1305, SALSA20_POLY1305)")]
    public CipherSuiteType EncryptionMode { get; set; } = CipherSuiteType.CHACHA20_POLY1305;
}