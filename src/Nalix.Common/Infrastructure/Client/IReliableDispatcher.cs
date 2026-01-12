// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Messaging.Packets.Abstractions;

namespace Nalix.Common.Infrastructure.Client;

/// <summary>
/// Simple packet dispatcher abstraction.
/// </summary>
public interface IReliableDispatcher : System.IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the dispatcher contains no registered handlers.
    /// </summary>
    System.Boolean IsEmpty { get; }

    /// <summary>
    /// Register a persistent handler for packets of type TPacket.
    /// </summary>
    void Register<TPacket>(System.Action<TPacket> handler) where TPacket : class, IPacket;

    /// <summary>
    /// Register a one-shot handler: handler invoked once when a packet matching predicate arrives.
    /// The handler is removed after the first matching packet.
    /// </summary>
    void RegisterOnce<TPacket>(System.Func<TPacket, System.Boolean> predicate, System.Action<TPacket> handler) where TPacket : class, IPacket;

    /// <summary>
    /// Dispatch an incoming packet to registered handlers.
    /// </summary>
    void Dispatch(IPacket packet);
}