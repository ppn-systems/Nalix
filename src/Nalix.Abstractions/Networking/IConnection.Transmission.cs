// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Abstractions.Networking;

public partial interface IConnection
{
    /// <summary>
    /// Gets the Transmission CONTROL number (TCP) transmission interface
    /// </summary>
    ITransport TCP { get; }

    /// <summary>
    /// Gets the USER Datagram number (UDP) transmission interface
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

        /// <summary>
        /// Detaches the underlying OS socket from the transport engine for TCP connections.
        /// This allows the caller to take ownership of the socket without it being disposed by the connection.
        /// </summary>
        /// <returns>The detached <see cref="System.Net.Sockets.Socket"/>, if supported.</returns>
        /// <exception cref="NotSupportedException">Thrown if the transport does not support socket unwrapping.</exception>
        System.Net.Sockets.Socket Unwrap();
    }
}
