// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;

namespace Nalix.Common.Client;

/// <summary>
/// Represents a reliable client connection to a server, providing events for connection lifecycle and packet reception.
/// </summary>
public interface IReliableClient : System.IDisposable
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

    /// <summary>
    /// Gets a value indicating whether the client has completed the handshake process with the server.
    /// </summary>
    System.Boolean IsHandshaked { get; }

    #endregion Properties

    #region Events

    /// <summary>
    /// Raised after a successful connection is established.
    /// Executed on the calling thread of ConnectAsync.
    /// </summary>
    event System.Action Connected;

    /// <summary>
    /// Raised whenever a packet is received on the background network worker.
    /// Executed on a background thread; do not touch Unity API here.
    /// </summary>
    event System.Action<IPacket> PacketReceived;

    /// <summary>
    /// Raised when the connection is closed or the receive loop exits due to an error.
    /// Executed on a background thread; ex is null for normal Dispose().
    /// </summary>
    event System.Action<System.Exception> Disconnected;

    #endregion Events

    #region Methods

    /// <summary>
    /// Asynchronously connects to the server with a specified timeout and cancellation token.
    /// </summary>
    /// <param name="timeout">The maximum time, in milliseconds, to wait for the connection to be established. Default is 30,000 ms.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Default is none.</param>
    /// <returns>A task representing the asynchronous connection operation.</returns>
    System.Threading.Tasks.Task ConnectAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 timeout = 30000,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default);

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
    System.Threading.Tasks.Task SendAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] IPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken ct = default);

    /// <summary>
    /// Disconnects the client from the server and releases all associated resources.
    /// </summary>
    void Disconnect();

    #endregion Methods
}