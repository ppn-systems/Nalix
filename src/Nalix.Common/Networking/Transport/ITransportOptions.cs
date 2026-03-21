// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security;

namespace Nalix.Common.Networking.Transport;

/// <summary>
/// Defines the full set of options required to configure a transport connection.
/// </summary>
/// <remarks>
/// All extension methods (<c>HandshakeExtensions</c>, <c>TimeSyncExtensions</c>,
/// <c>DirectiveClientExtensions</c>, etc.) depend only on this interface — never on
/// the concrete <c>TransportOptions</c> class — so they remain testable and mockable.
/// </remarks>
public interface ITransportOptions
{
    // ── Endpoint ──────────────────────────────────────────────────────────

    /// <summary>Port number for the connection.</summary>
    System.UInt16 Port { get; set; }

    /// <summary>Server address or hostname.</summary>
    System.String Address { get; set; }

    // ── Connect ───────────────────────────────────────────────────────────

    /// <summary>
    /// Timeout for connect attempts in milliseconds.
    /// <c>0</c> means no timeout.
    /// </summary>
    System.Int32 ConnectTimeoutMillis { get; set; }

    // ── Reconnect ─────────────────────────────────────────────────────────

    /// <summary>
    /// When <c>true</c>, the client automatically reconnects after an unexpected disconnect.
    /// </summary>
    System.Boolean ReconnectEnabled { get; set; }

    /// <summary>
    /// Maximum number of reconnect attempts. <c>0</c> = unlimited.
    /// </summary>
    System.Int32 ReconnectMaxAttempts { get; set; }

    /// <summary>
    /// Base delay in milliseconds for exponential backoff between reconnect attempts.
    /// </summary>
    System.Int32 ReconnectBaseDelayMillis { get; set; }

    /// <summary>
    /// Maximum delay in milliseconds between reconnect attempts.
    /// </summary>
    System.Int32 ReconnectMaxDelayMillis { get; set; }

    // ── Keep-alive ────────────────────────────────────────────────────────

    /// <summary>
    /// Interval in milliseconds to send keep-alive (heartbeat) packets.
    /// <c>0</c> disables heartbeats.
    /// </summary>
    System.Int32 KeepAliveIntervalMillis { get; set; }

    // ── Socket tuning ─────────────────────────────────────────────────────

    /// <summary>
    /// Controls the <c>TCP_NODELAY</c> socket option.
    /// When <c>true</c>, Nagle's algorithm is disabled for lower latency.
    /// </summary>
    System.Boolean NoDelay { get; set; }

    /// <summary>
    /// Size in bytes of the socket send and receive buffer.
    /// </summary>
    System.Int32 BufferSize { get; set; }

    // ── Limits ────────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum allowed packet size (header + payload) in bytes.
    /// </summary>
    System.Int32 MaxPacketSize { get; set; }

    // ── Security ──────────────────────────────────────────────────────────

    /// <summary>
    /// Encryption key used for secure communication.
    /// Set by <c>HandshakeExtensions</c> after the X25519 key exchange completes.
    /// </summary>
    System.Byte[] Secret { get; set; }

    /// <summary>
    /// Cipher suite used for encrypting packets.
    /// </summary>
    CipherSuiteType Algorithm { get; set; }
}