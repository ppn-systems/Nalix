// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Packets;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Provides a zero-reflection bridge to transition from a generic IPacket context
/// to a strongly-typed context for specific packet handlers.
/// </summary>
public static class PacketContextBridge
{
    private static readonly ObjectPoolManager s_pool = ObjectPoolManager.Shared;

    /// <summary>
    /// Creates a strongly-typed packet context by renting from the object pool
    /// and initializing it with the properties of the base context.
    /// </summary>
    /// <typeparam name="TConcrete">The concrete packet type.</typeparam>
    /// <typeparam name="TBase">The base packet type of the pipeline.</typeparam>
    /// <param name="baseContext">The original generic packet context.</param>
    /// <param name="concretePacket">The downcasted concrete packet.</param>
    /// <returns>A rented, initialized packet context for the specific type.</returns>
    public static PacketContext<TConcrete> Create<TConcrete, TBase>(PacketContext<TBase> baseContext, TConcrete concretePacket)
        where TConcrete : IPacket, new()
        where TBase : IPacket
    {
        System.ArgumentNullException.ThrowIfNull(baseContext);

        PacketContext<TConcrete> bridgeContext = s_pool.Get<PacketContext<TConcrete>>();

        // Call the internal Initialize method within Nalix.Runtime boundary
        bridgeContext.Initialize(
            concretePacket,
            baseContext.Connection,
            baseContext.Attributes,
            baseContext.IsReliable,
            baseContext.EncryptedOnWire,
            ownsPacket: false,
            baseContext.CancellationToken);

        return bridgeContext;
    }

    /// <summary>
    /// Returns a bridge context to the object pool without disposing the borrowed packet.
    /// The original base context retains packet ownership and is responsible for its disposal.
    /// </summary>
    /// <typeparam name="TConcrete">The concrete packet type.</typeparam>
    /// <param name="bridgeContext">The bridge context to return.</param>
    public static void Return<TConcrete>(PacketContext<TConcrete> bridgeContext)
        where TConcrete : IPacket, new()
    {
        System.ArgumentNullException.ThrowIfNull(bridgeContext);
        bridgeContext.Dispose();
    }
}
