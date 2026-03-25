// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;

namespace Nalix.Common.Networking.Transport;

/// <summary>
/// Represents a reliable client connection to a server.
/// </summary>
public interface IClientConnection : IDisposable
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
    bool IsConnected { get; }

    /// <summary>
    /// Gets the packet registry (catalog) used to resolve and manage
    /// packet types and their associated handlers for this connection.
    /// </summary>
    /// <remarks>
    /// This registry acts as a central catalog for all packet definitions,
    /// enabling serialization, deserialization, and dispatching of packets.
    /// </remarks>
    IPacketRegistry Catalog { get; }

    #endregion Properties

    #region Events

    /// <summary>Occurs when the client successfully connects to the remote endpoint.</summary>
    event EventHandler OnConnected;

    /// <summary>
    /// Occurs when the client disconnects.
    /// The argument is the exception that caused the disconnect, or <c>null</c> if it was requested.
    /// </summary>
    event EventHandler<Exception> OnDisconnected;

    /// <summary>
    /// Synchronous message-received event.
    /// Each subscriber receives its own <see cref="IBufferLease"/> copy and is responsible
    /// for disposing it exactly once.
    /// </summary>
    event EventHandler<IBufferLease> OnMessageReceived;

    /// <summary>Occurs when bytes are written to the socket. Argument = byte count sent.</summary>
    event EventHandler<long> OnBytesSent;

    /// <summary>Occurs when bytes are received from the socket. Argument = byte count (header+payload).</summary>
    event EventHandler<long> OnBytesReceived;

    /// <summary>Occurs when an internal error happens — useful for logging and diagnostics.</summary>
    event EventHandler<Exception> OnError;

    #endregion Events

    #region Methods

    /// <summary>
    /// Connects to the specified host and port asynchronously.
    /// Stores host/port for automatic reconnects.
    /// </summary>
    /// <param name="host">
    /// The remote host name or address to connect to.
    /// </param>
    /// <param name="port">
    /// The remote port to connect to.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that can cancel the connection attempt.
    /// </param>
    Task ConnectAsync(
        string host = null,
        ushort? port = null,
        CancellationToken ct = default);

    /// <summary>Disconnects and cancels all background loops.</summary>
    Task DisconnectAsync();

    /// <summary>Serializes and sends <paramref name="packet"/> asynchronously.</summary>
    /// <param name="packet">
    /// The packet to serialize and send.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that can cancel the send operation.
    /// </param>
    /// <returns><c>true</c> if sent successfully; <c>false</c> on socket error.</returns>
    Task<bool> SendAsync(
        IPacket packet,
        CancellationToken ct = default);

    /// <summary>Sends a raw payload (without framing header) asynchronously.</summary>
    /// <param name="payload">
    /// The raw payload bytes to send.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that can cancel the send operation.
    /// </param>
    /// <returns><c>true</c> if sent successfully; <c>false</c> on socket error.</returns>
    Task<bool> SendAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken ct = default);

    #endregion Methods
}
