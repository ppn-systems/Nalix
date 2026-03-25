// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;

namespace Nalix.Common.Networking.Transport;

/// <summary>
/// Simple packet dispatcher abstraction.
/// </summary>
public interface IPacketDispatcher : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the dispatcher contains no registered handlers.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Dispatch an incoming packet to registered handlers.
    /// </summary>
    /// <param name="packet">
    /// The packet instance to dispatch to registered handlers.
    /// </param>
    void Dispatch(IPacket packet);

    /// <summary>
    /// Register a persistent handler for packets of type TPacket.
    /// </summary>
    /// <typeparam name="TPacket">The packet type handled by the delegate.</typeparam>
    /// <param name="handler">
    /// The handler to invoke for packets of type <typeparamref name="TPacket"/>.
    /// </param>
    void Register<TPacket>(Action<TPacket> handler) where TPacket : class, IPacket;

    /// <summary>
    /// Register a one-shot handler: handler invoked once when a packet matching predicate arrives.
    /// The handler is removed after the first matching packet.
    /// </summary>
    /// <typeparam name="TPacket">The packet type handled by the delegate.</typeparam>
    /// <param name="predicate">
    /// A predicate that determines whether the incoming packet matches.
    /// </param>
    /// <param name="handler">
    /// The handler to invoke once when a matching packet arrives.
    /// </param>
    void RegisterOnce<TPacket>(Func<TPacket, bool> predicate, Action<TPacket> handler) where TPacket : class, IPacket;
}
