// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;

namespace Nalix.Common.Connection;

public partial interface IConnection
{
    /// <summary>
    /// Gets the Transmission CONTROL ProtocolType (TCP) transmission interface
    /// </summary>
    ITcp TCP { get; }

    /// <summary>
    /// Gets the User Datagram ProtocolType (UDP) transmission interface
    /// </summary>
    IUdp UDP { get; }

    /// <summary>
    /// Represents a transport interface for sending data packets.
    /// </summary>
    interface ITransport
    {
        /// <summary>
        /// Sends a packet synchronously over the connection.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <returns></returns>
        System.Boolean Send(IPacket packet);

        /// <summary>
        /// Sends a message synchronously over the connection.
        /// </summary>
        /// <param name="message">The message to send.</param>
        System.Boolean Send(System.ReadOnlySpan<System.Byte> message);

        /// <summary>
        /// Sends a message asynchronously over the connection.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="cancellationToken">A token to cancel the sending operation.</param>
        /// <returns>A task that represents the asynchronous sending operation.</returns>
        /// <remarks>
        /// If the connection has been authenticated, the data will be encrypted before sending.
        /// </remarks>
        System.Threading.Tasks.Task<System.Boolean> SendAsync(
            IPacket packet,
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message asynchronously over the connection.
        /// </summary>
        /// <param name="message">The data to send.</param>
        /// <param name="cancellationToken">A token to cancel the sending operation.</param>
        /// <returns>A task that represents the asynchronous sending operation.</returns>
        /// <remarks>
        /// If the connection has been authenticated, the data will be encrypted before sending.
        /// </remarks>
        System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.ReadOnlyMemory<System.Byte> message,
            System.Threading.CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the Transmission CONTROL ProtocolType (TCP) transmission interface
    /// for a network connection.
    /// </summary>
    /// <remarks>
    /// This interface inherits from <see cref="ITransport"/> which defines
    /// common send methods for sending packets or raw data synchronously
    /// and asynchronously.
    ///
    /// TCP is a connectionless protocol, so this interface focuses mainly
    /// on sending data without connection state management or receive control.
    /// </remarks>
    interface ITcp : ITransport
    {
        /// <summary>
        /// Starts receiving data from the connection.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to cancel the receiving operation.
        /// </param>
        /// <remarks>
        /// Call this method to initiate listening for incoming data on the connection.
        /// </remarks>
        void BeginReceive(System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message synchronously over the connection.
        /// </summary>
        /// <param name="message">The message to send.</param>
        System.Boolean Send(System.String message);

        /// <summary>
        /// Sends a message asynchronously over the connection.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">A token to cancel the sending operation.</param>
        /// <returns>A task that represents the asynchronous sending operation.</returns>
        /// <remarks>
        /// If the connection has been authenticated, the data will be encrypted before sending.
        /// </remarks>
        System.Threading.Tasks.Task<System.Boolean> SendAsync(
            System.String message,
            System.Threading.CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the User Datagram ProtocolType (UDP) transmission interface
    /// for a network connection.
    /// </summary>
    /// <remarks>
    /// This interface inherits from <see cref="ITransport"/> which defines
    /// common send methods for sending packets or raw data synchronously
    /// and asynchronously.
    ///
    /// UDP is a connectionless protocol, so this interface focuses mainly
    /// on sending data without connection state management or receive control.
    /// </remarks>
    interface IUdp : ITransport
    {
        /// <summary>
        /// Initializes the UDP transport with the specified outer <see cref="IConnection"/>.
        /// </summary>
        /// <param name="outer">The outer <see cref="IConnection"/> instance to associate with this UDP transport.</param>
        void Initialize(IConnection outer);
    }
}