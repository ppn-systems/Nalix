// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Common.Abstractions;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.Identifiers;

namespace Nalix.SDK.Options;

/// <summary>
/// Client transport configuration loaded by the ConfigurationManager.
/// Place appropriate keys in your configuration source under the section
/// matching this class name (TransportOptions).
/// </summary>
[IniComment("Client transport configuration — controls connectivity, reconnect policy, socket tuning, and encryption")]
public sealed class TransportOptions : ConfigurationLoader
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
    [Range(2048, 1048576, ErrorMessage = "BufferSize must be between 2048 and 1048576 bytes.")]
    public int BufferSize { get; set; } = 65536;

    /// <summary>
    /// Gets the encryption key used for secure communication.
    /// </summary>
    [ConfiguredIgnore]
    public Bytes32 Secret { get; set; }

    /// <summary>
    /// Gets the encryption mode for the connection.
    /// </summary>
    [IniComment("Cipher suite used for packet encryption (e.g., Chacha20, Salsa20, Chacha20Poly1305, Salsa20Poly1305)")]
    public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;

    /// <summary>
    /// When true, LZ4 compression is applied to packets exceeding the <see cref="CompressionThreshold"/>.
    /// </summary>
    [IniComment("Enable LZ4 compression for outbound packets")]
    public bool CompressionEnabled { get; set; } = true;

    /// <summary>
    /// The minimum size in bytes for a packet to be considered for compression.
    /// </summary>
    [IniComment("Minimum size in bytes to trigger compression")]
    public int CompressionThreshold { get; set; } = 512;

    /// <summary>
    /// When true, AEAD encryption is applied to all outbound packets.
    /// </summary>
    [IniComment("Enable packet encryption (secure by default — disable only for unencrypted dev/test environments)")]
    public bool EncryptionEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the capacity of the asynchronous message processing queue.
    /// SEC-56, SEC-59: Provides backpressure to prevent memory exhaustion under high load.
    /// </summary>
    [IniComment("Capacity of the asynchronous message processing queue (default 1024)")]
    [Range(1, 65536, ErrorMessage = "AsyncQueueCapacity must be between 1 and 65536.")]
    public int AsyncQueueCapacity { get; set; } = 1024;

    /// <summary>
    /// The maximum size (in bytes) allowed for a single UDP datagram (including the 8-byte Token).
    /// </summary>
    [IniComment("Maximum allowed UDP datagram size in bytes (including header). Default 1400.")]
    [Range(64, 65507, ErrorMessage = "MaxUdpDatagramSize must be between 64 and 65507.")]
    public int MaxUdpDatagramSize { get; set; } = 1400;

    /// <summary>
    /// Gets the unique session token assigned by the server for UDP communication.
    /// </summary>
    [ConfiguredIgnore]
    public Snowflake SessionToken { get; set; }

    /// <summary>
    /// Gets or sets the pinned Server Identity Public Key (required for protecting against MitM attacks).
    /// </summary>
    [IniComment("Pinned X25519 Public Key representation in HEX for Identity Authentication (MitM protection).")]
    public string? ServerPublicKey { get; set; }

    /// <summary>
    /// Gets or sets whether the SDK should attempt a session resume before performing a new handshake.
    /// </summary>
    [IniComment("Attempt session resume before doing a fresh handshake")]
    public bool ResumeEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the resume request timeout in milliseconds.
    /// </summary>
    [IniComment("Timeout in milliseconds for session resume requests")]
    [Range(100, int.MaxValue, ErrorMessage = "ResumeTimeoutMillis must be at least 100.")]
    public int ResumeTimeoutMillis { get; set; } = 3000;

    /// <summary>
    /// Gets or sets whether a failed resume should fall back to a fresh handshake.
    /// </summary>
    [IniComment("Fall back to a full handshake when resume fails")]
    public bool ResumeFallbackToHandshake { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the internal global clock can be synchronized with the server's time during SyncTimeAsync.
    /// Disable this if the connection is untrusted to prevent cross-session clock skew.
    /// </summary>
    [IniComment("Allow global clock synchronization from this session (recommended: true for trusted servers)")]
    public bool TimeSyncEnabled { get; set; } = true;
}
