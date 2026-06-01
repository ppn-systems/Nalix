// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking;

public partial interface IConnection
{
    /// <summary>
    /// Gets the Transmission CONTROL number (TCP) transmission interface.
    /// For socket-based connections, cast to <see cref="ISocketTransport"/> for direct socket access.
    /// </summary>
    ITransport TCP { get; }

    /// <summary>
    /// Gets the USER Datagram number (UDP) transmission interface.
    /// </summary>
    ITransport UDP { get; }

    /// <summary>
    /// Represents a transport interface for sending data packets.
    /// </summary>
    interface ITransport : ITransportSequencer
    {
        /// <summary>
        /// Gets the framing strategy used by this protocol.
        /// </summary>
        TransportFraming Framing { get; }

        /// <summary>
        /// Sends a message synchronously over the connection.
        /// </summary>
        /// <param name="message">The message to send.</param>
        void Send(ReadOnlySpan<byte> message);

        /// <summary>
        /// Sends a message asynchronously over the connection.
        /// </summary>
        /// <param name="message">The data to send.</param>
        /// <param name="cancellationToken">A token to cancel the sending operation.</param>
        /// <returns>A task that represents the asynchronous sending operation.</returns>
        /// <remarks>
        /// If the connection has been authenticated, the data will be encrypted before sending.
        /// </remarks>
        ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts receiving data from the connection.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to cancel the receiving operation.
        /// </param>
        /// <remarks>
        /// Call this method to initiate listening for incoming data on the connection.
        /// </remarks>
        void BeginReceive(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the framing mode for this transport.
        /// Must be called before <see cref="BeginReceive"/> is invoked.
        /// </summary>
        /// <param name="framing">The framing mode to use.</param>
        void UseFraming(TransportFraming framing);
    }

    /// <summary>
    /// Extends <see cref="ITransport"/> with direct access to the underlying OS socket.
    /// Only applicable to socket-based transports (e.g., TCP).
    /// </summary>
    interface ISocketTransport : ITransport
    {
        /// <summary>
        /// Gets the underlying <see cref="System.Net.Sockets.Socket"/> used by this transport.
        /// </summary>
        System.Net.Sockets.Socket Socket { get; }

        /// <summary>
        /// Gets the task representing the receive loop.
        /// Used by unwrapped connections to await loop shutdown and recover stolen packets.
        /// </summary>
        Task? ReceiveLoopTask { get; }

        /// <summary>
        /// Contains bytes that were read from the kernel but not processed by the framework
        /// due to <see cref="Unwrap"/> or cancellation.
        /// </summary>
        byte[]? StolenData { get; }

        /// <summary>
        /// Detaches the underlying OS socket from the transport engine.
        /// This allows the caller to take ownership of the socket without it being disposed by the connection.
        /// </summary>
        /// <returns>The detached <see cref="System.Net.Sockets.Socket"/>.</returns>
        System.Net.Sockets.Socket Unwrap();
    }
}
