﻿using Nalix.Common.Connection;
using Nalix.Common.Packets;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// Defines the contract for a dispatch channel that manages the queuing, retrieval, and association of packets with connections.
/// </summary>
/// <typeparam name="TPacket">The type of packet used in the dispatch channel, which must implement the <see cref="IPacket"/> interface.</typeparam>
public interface IDispatchChannel<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Gets the current number of packets in the dispatch queue.
    /// </summary>
    /// <value>The number of packets currently enqueued.</value>
    System.Int32 TotalPackets { get; }

    /// <summary>
    /// Adds a packet to the dispatch queue, associating it with a specific connection.
    /// </summary>
    /// <param name="packet">The packet to be added to the queue.</param>
    /// <param name="connection">The connection associated with the packet.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="packet"/> or <paramref name="connection"/> is null.</exception>
    void Push(TPacket packet, IConnection connection);

    /// <summary>
    /// Attempts to retrieve a packet and its associated connection from the dispatch queue.
    /// </summary>
    /// <param name="packet">When this method returns, contains the retrieved packet, or the default value of <typeparamref name="TPacket"/> if the queue is empty.</param>
    /// <param name="connection">When this method returns, contains the connection associated with the retrieved packet, or null if the queue is empty.</param>
    /// <returns><c>true</c> if a packet was successfully retrieved; otherwise, <c>false</c> if the queue is empty.</returns>
    System.Boolean Pull(out TPacket packet, out IConnection connection);
}