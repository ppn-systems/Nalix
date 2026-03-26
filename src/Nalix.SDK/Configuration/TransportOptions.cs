// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.Common.Security;
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
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public ushort Port { get; set; } = 57206;

    /// <summary>
    /// Gets the server address or hostname.
    /// </summary>
    [IniComment("Server IP address or hostname")]
    [Required(ErrorMessage = "Address is required.")]
    public string Address { get; set; } = "127.0.0.1";

    // Basic connectivity

    /// <summary>
    /// Timeout for connect attempts in milliseconds. A value of 0 means no timeout.
    /// </summary>
    [IniComment("Connect attempt timeout in milliseconds (0 = no timeout)")]
    [Range(0, int.MaxValue, ErrorMessage = "ConnectTimeoutMillis must be non-negative.")]
    public int ConnectTimeoutMillis { get; set; } = 5000;

    /// <summary>
    /// When true, automatic reconnect is enabled following an unexpected disconnect.
    /// </summary>
    [IniComment("Automatically reconnect after an unexpected disconnect")]
    public bool ReconnectEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of reconnect attempts. 0 indicates unlimited attempts.
    /// </summary>
    [IniComment("Max reconnect attempts (0 = unlimited)")]
    [Range(0, int.MaxValue, ErrorMessage = "ReconnectMaxAttempts must be non-negative.")]
    public int ReconnectMaxAttempts { get; set; }

    /// <summary>
    /// Base delay (in milliseconds) used for exponential backoff between reconnect attempts.
    /// </summary>
    [IniComment("Base delay in milliseconds for exponential backoff between reconnect attempts")]
    [Range(0, 30000, ErrorMessage = "ReconnectBaseDelayMillis must be between 0 and 30000.")]
    public int ReconnectBaseDelayMillis { get; set; } = 500;

    /// <summary>
    /// Maximum delay (in milliseconds) allowed between reconnect attempts.
    /// </summary>
    [IniComment("Maximum delay in milliseconds between reconnect attempts")]
    [Range(0, 30000, ErrorMessage = "ReconnectMaxDelayMillis must be between 0 and 30000.")]
    public int ReconnectMaxDelayMillis { get; set; } = 30000;

    // Keep-alive / heartbeat (ms). 0 = disabled.

    /// <summary>
    /// Interval in milliseconds to send keep-alive (heartbeat) packets. 0 disables heartbeats.
    /// </summary>
    [IniComment("Heartbeat interval in milliseconds (0 = disabled)")]
    [Range(0, int.MaxValue, ErrorMessage = "KeepAliveIntervalMillis must be non-negative.")]
    public int KeepAliveIntervalMillis { get; set; } = 20_000;

    // Socket tuning

    /// <summary>
    /// Controls the TCP_NODELAY socket option. When true, Nagle's algorithm is disabled.
    /// </summary>
    [IniComment("Disable Nagle's algorithm for lower latency (recommended: true)")]
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// Size (in bytes) of the socket send and receive buffer.
    /// </summary>
    [IniComment("Socket send and receive buffer size in bytes")]
    [Range(1024, 1048576, ErrorMessage = "BufferSize must be between 1024 and 1048576 bytes.")]
    public int BufferSize { get; set; } = 8192;

    // Limits

    /// <summary>
    /// Maximum allowed packet size (header + payload) in bytes.
    /// </summary>
    [IniComment("Maximum packet size in bytes including header and payload")]
    [Range(512, 65536, ErrorMessage = "MaxPacketSize must be between 512 and 65536 bytes.")]
    public int MaxPacketSize { get; set; } = 64 * 1024;

    /// <summary>
    /// Enable or disable compression for outgoing packets. Compression is applied to packets larger than MinSizeToCompress.
    /// </summary>
    [IniComment("Enable compression for outgoing packets (only applies to packets larger than MinSizeToCompress)")]
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Minimum packet size in bytes required to trigger compression. Packets smaller than this threshold will not be compressed.
    /// </summary>
    [IniComment("Minimum packet size in bytes to trigger compression (only applies if EnableCompression is true)")]
    public int MinSizeToCompress { get; set; } = 1024;

    /// <summary>
    /// Gets the encryption key used for secure communication.
    /// </summary>
    [ConfiguredIgnore]
    public byte[] Secret { get; set; } = [];

    /// <summary>
    /// Gets the encryption mode for the connection.
    /// </summary>
    [IniComment("Cipher suite used for packet encryption (e.g., Chacha20, Salsa20, Chacha20Poly1305, Salsa20Poly1305)")]
    public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        if (this.ReconnectBaseDelayMillis > this.ReconnectMaxDelayMillis)
        {
            throw new ValidationException("ReconnectBaseDelayMillis cannot be greater than ReconnectMaxDelayMillis.");
        }
        if (this.BufferSize is < 1024 or > 1048576)
        {
            throw new ValidationException("BufferSize must be between 1024 and 1048576 bytes.");
        }
        if (this.MaxPacketSize is < 512 or > 65536)
        {
            throw new ValidationException("MaxPacketSize must be between 512 and 65536 bytes.");
        }
    }
}
