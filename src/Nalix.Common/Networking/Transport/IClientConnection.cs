// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;

namespace Nalix.Common.Networking.Transport;

/// <summary>
/// Represents a reliable client connection to a server.
/// </summary>
public interface IClientConnection : System.IDisposable
{
    #region Properties

    /// <summary>
    /// Gets the transport options used by the client.
    /// Exposes the full <see cref="ITransportOptions"/> so extension methods can read
    /// timeouts, reconnect settings, and limits without casting to a concrete type.
    /// </summary>
    ITransportOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether the client is currently connected to the server.
    /// </summary>
    System.Boolean IsConnected { get; }

    #endregion Properties

    #region Events

    /// <summary>Occurs when the client successfully connects to the remote endpoint.</summary>
    event System.EventHandler OnConnected;

    /// <summary>
    /// Occurs when the client disconnects.
    /// The argument is the exception that caused the disconnect, or <c>null</c> if it was requested.
    /// </summary>
    event System.EventHandler<System.Exception> OnDisconnected;

    /// <summary>
    /// Synchronous message-received event.
    /// Each subscriber receives its own <see cref="IBufferLease"/> copy and is responsible
    /// for disposing it exactly once.
    /// </summary>
    event System.EventHandler<IBufferLease> OnMessageReceived;

    /// <summary>Occurs when bytes are written to the socket. Argument = byte count sent.</summary>
    event System.EventHandler<System.Int64> OnBytesSent;

    /// <summary>Occurs when bytes are received from the socket. Argument = byte count (header+payload).</summary>
    event System.EventHandler<System.Int64> OnBytesReceived;

    /// <summary>Occurs when an internal error happens — useful for logging and diagnostics.</summary>
    event System.EventHandler<System.Exception> OnError;

    #endregion Events

    #region Methods

    /// <summary>
    /// Connects to the specified host and port asynchronously.
    /// Stores host/port for automatic reconnects.
    /// </summary>
    System.Threading.Tasks.Task ConnectAsync(
        System.String host = null,
        System.UInt16? port = null,
        System.Threading.CancellationToken ct = default);

    /// <summary>Disconnects and cancels all background loops.</summary>
    System.Threading.Tasks.Task DisconnectAsync();

    /// <summary>Serializes and sends <paramref name="packet"/> asynchronously.</summary>
    /// <returns><c>true</c> if sent successfully; <c>false</c> on socket error.</returns>
    System.Threading.Tasks.Task<System.Boolean> SendAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] IPacket packet,
        System.Threading.CancellationToken ct = default);

    /// <summary>Sends a raw payload (without framing header) asynchronously.</summary>
    /// <returns><c>true</c> if sent successfully; <c>false</c> on socket error.</returns>
    System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.ReadOnlyMemory<System.Byte> payload,
        System.Threading.CancellationToken ct = default);

    #endregion Methods
}