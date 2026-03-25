// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;

namespace Nalix.Network.Routing;

/// <summary>
/// Defines the contract for a dispatch channel that manages the queuing, retrieval,
/// and association of packets with connections.
/// </summary>
public interface IDispatchChannel<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Gets the current number of packets in the dispatch queue.
    /// </summary>
    /// <value>
    /// The total number of packets currently enqueued for processing.
    /// </value>
    System.Int64 TotalPackets { get; }

    /// <summary>
    /// Adds a packet to the dispatch queue, associating it with a specific connection.
    /// </summary>
    /// <param name="connection">
    /// The connection associated with the packet.
    /// </param>
    /// <param name="raw">
    /// The packet to be added to the queue.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="raw"/> or <paramref name="connection"/> is <see langword="null"/>.
    /// </exception>
    void Push(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNull] IBufferLease raw);

    /// <summary>
    /// Attempts to retrieve a packet and its associated connection from the dispatch queue.
    /// </summary>
    /// <param name="connection">
    /// When this method returns, contains the connection associated with the retrieved packet,
    /// or <see langword="null"/> if the queue is empty.
    /// </param>
    /// <param name="raw">
    /// When this method returns, contains the retrieved packet,
    /// or the default value of <typeparamref name="TPacket"/> if the queue is empty.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a packet was successfully retrieved; otherwise,
    /// <see langword="false"/> if the queue is empty.
    /// </returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    System.Boolean Pull(
        [System.Diagnostics.CodeAnalysis.NotNull] out IConnection connection,
        [System.Diagnostics.CodeAnalysis.MaybeNull] out IBufferLease raw);
}