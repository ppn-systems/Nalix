// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Messaging.Packets.Abstractions;

namespace Nalix.Common.Infrastructure.Client;

/// <summary>
/// Represents a reliable client connection to a server, providing events for connection lifecycle and packet reception.
/// </summary>
public interface IClientConnection : System.IDisposable
{
    #region Properties

    /// <summary>
    /// Gets the transport options used by the client.
    /// </summary>
    ITransportOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    System.Boolean IsConnected { get; }

    #endregion Properties

    #region Events

    // Events
    /// <summary>
    /// Occurs when the client has successfully connected to the remote endpoint.
    /// </summary>
    event System.EventHandler OnConnected;

    /// <summary>
    /// Occurs when the client is disconnected. The <see cref="System.EventHandler{T}"/> argument
    /// contains the exception that caused the disconnect, or <c>null</c> if it was requested.
    /// </summary>
    event System.EventHandler<System.Exception> OnDisconnected;

    /// <summary>
    /// Synchronous message-received event. Subscribers receive an <see cref="IBufferLease"/>
    /// and are responsible for disposing the lease when done.
    /// </summary>
    event System.EventHandler<IBufferLease> OnMessageReceived;

    /// <summary>
    /// Occurs when bytes are written to the socket. The event argument is the number of bytes sent.
    /// </summary>
    event System.EventHandler<System.Int64> OnBytesSent;

    /// <summary>
    /// Occurs when bytes are received from the socket. The event argument is the number of bytes (header+payload) received for that frame.
    /// </summary>
    event System.EventHandler<System.Int64> OnBytesReceived;

    /// <summary>
    /// Occurs when an internal error happens. Subscribers can use this for logging or diagnostics.
    /// </summary>
    event System.EventHandler<System.Exception> OnError;

    #endregion Events

    #region Methods

    /// <summary>
    /// Connects to the specified host and port asynchronously.
    /// This method stores host/port for automatic reconnects.
    /// </summary>
    /// <param name="host">The hostname or IP address to connect to.</param>
    /// <param name="port">The destination port.</param>
    /// <param name="ct">A <see cref="System.Threading.CancellationToken"/> to cancel the connect attempt.</param>
    /// <returns>A task that completes when connected.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="host"/> is null or whitespace.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when the client has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the client is already connected.</exception>
    System.Threading.Tasks.Task ConnectAsync(System.String host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default);

    /// <summary>Disconnects the client and cancels background loops.</summary>
    /// <returns>A completed task when disconnect work is initiated.</returns>
    /// <remarks>
    /// This is a best-effort, synchronous-style disconnect that cancels background loops and
    /// disposes the underlying socket. It is safe to call multiple times.
    /// </remarks>
    System.Threading.Tasks.Task DisconnectAsync();

    /// <summary>
    /// Asynchronously sends a packet to the server.
    /// </summary>
    /// <param name="packet">The packet to send. Must not be null.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete. Default is none.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the client is not connected.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if <paramref name="ct"/> is canceled before or during the send.
    /// </exception>
    /// <exception cref="System.IO.IOException">
    /// Thrown if an IO error occurs while writing to the underlying stream.
    /// </exception>
    System.Threading.Tasks.Task<System.Boolean> SendAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] IPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken ct = default);

    /// <summary>
    /// Sends a framed payload (header + payload) asynchronously.
    /// </summary>
    /// <param name="payload">Payload bytes to send (payload only, header is added by the protocol).</param>
    /// <param name="ct">Cancellation token to cancel the send operation.</param>
    /// <returns>Task that resolves to <c>true</c> if the send succeeded, otherwise <c>false</c>.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if the client has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the client is not connected.</exception>
    System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken ct = default);

    #endregion Methods
}