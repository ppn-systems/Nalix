// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Defines an exclusive processing session for a single connection's mailbox.
/// While active, the holder has sole ownership of the connection's packet queue,
/// ensuring strict in-order delivery without cross-worker interference.
/// </summary>
public interface IDispatchSession : IDisposable
{
    /// <summary>
    /// Gets the connection claimed by this session.
    /// </summary>
    IConnection Connection { get; }

    /// <summary>
    /// Attempts to dequeue the next highest-priority packet from the claimed connection.
    /// </summary>
    /// <param name="raw">
    /// When this method returns <see langword="true"/>, contains the dequeued packet lease.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a packet was dequeued; <see langword="false"/> if the
    /// connection's mailbox is empty.
    /// </returns>
    bool TryDequeue([NotNullWhen(true)] out IBufferLease raw);
}

/// <summary>
/// Defines the contract for a dispatch channel that manages the queuing, retrieval,
/// and association of packets with connections.
/// </summary>
/// <typeparam name="TPacket"></typeparam>
public interface IDispatchChannel<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Gets the current number of packets in the dispatch queue.
    /// </summary>
    /// <value>
    /// The total number of packets currently enqueued for processing.
    /// </value>
    long TotalPackets { get; }

    /// <summary>
    /// Adds a packet to the dispatch queue, associating it with a specific connection.
    /// </summary>
    /// <param name="connection">
    /// The connection associated with the packet.
    /// </param>
    /// <param name="raw">
    /// The packet to be added to the queue.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="raw"/> or <paramref name="connection"/> is <see langword="null"/>.
    /// </exception>
    void Push(IConnection connection, IBufferLease raw);

    /// <summary>
    /// Attempts to claim exclusive processing rights over a connection's mailbox.
    /// Returns a session that allows the caller to dequeue packets sequentially.
    /// Disposing the session releases the claim and re-enqueues the connection
    /// if it still has pending packets.
    /// </summary>
    /// <param name="session">
    /// When this method returns <see langword="true"/>, contains the exclusive dispatch session.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a connection was claimed; <see langword="false"/> if no
    /// connection has pending packets.
    /// </returns>
    bool TryClaim([NotNullWhen(true)] out IDispatchSession session);
}
