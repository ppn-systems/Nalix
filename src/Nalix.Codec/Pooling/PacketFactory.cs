// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;

namespace Nalix.Codec.Pooling;

/// <summary>
/// Provides a high-performance, type-specific pool for packet instances.
/// Integrates with <see cref="IObjectPoolManager"/> and supports <see cref="PacketScope{TPacket}"/>.
/// </summary>
/// <typeparam name="TPacket">The packet type.</typeparam>
[SuppressMessage(
    "Design", "CA1000:Do not declare static members on generic types", Justification = "A generic packet pool is intentionally exposed as a per-packet-type static API.")]
public static class PacketFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TPacket> where TPacket : PacketBase<TPacket>, IPacketStaticOpcode, new()
{
    #region APIs

    /// <summary>
    /// Rents a packet and returns a zero-allocation lease. 
    /// The packet will be returned to the pool when the lease is disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PacketScope<TPacket> Acquire() => new(PacketBase<TPacket>.Create());

    /// <summary>
    /// Rents a batch of packets and returns a zero-allocation lease holding all instances. 
    /// The packets will be returned to the pool when the batch lease is disposed.
    /// </summary>
    /// <param name="count">The number of packets to rent.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PacketBatchScope<TPacket> AcquireBatch(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Batch count must be greater than zero.");
        }

        return new PacketBatchScope<TPacket>(count);
    }

    #endregion APIs
}
