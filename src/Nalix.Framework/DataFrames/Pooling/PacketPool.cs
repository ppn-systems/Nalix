// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Framework.DataFrames.Pooling;

/// <summary>
/// Provides a high-performance, type-specific pool for packet instances.
/// Integrates with <see cref="ObjectPoolManager"/> and supports <see cref="PacketLease{TPacket}"/>.
/// </summary>
/// <typeparam name="TPacket">The packet type.</typeparam>
[SuppressMessage(
    "Design", "CA1000:Do not declare static members on generic types", Justification = "A generic packet pool is intentionally exposed as a per-packet-type static API.")]
public static class PacketPool<TPacket> where TPacket : PacketBase<TPacket>, new()
{
    #region Fields

    /// <summary>
    /// Cached typed pool reference for maximum performance (eliminates dictionary lookups in hot paths).
    /// </summary>
    private static readonly TypedObjectPool<TPacket> s_pool =
        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().GetTypedPool<TPacket>();

    #endregion Fields

    #region APIs

    /// <summary>
    /// Rents a packet and returns a zero-allocation lease. 
    /// The packet will be returned to the pool when the lease is disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PacketLease<TPacket> Rent() => new(s_pool.Get());

    /// <summary>
    /// Rents a packet instance directly. 
    /// The caller is responsible for calling <c>Dispose()</c> on the returned packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPacket Get() => s_pool.Get();

    /// <summary>
    /// Preallocates instances for this packet type to reduce cold-start latency.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Prealloc(int count) => s_pool.Prealloc(count);

    /// <summary>
    /// Clears all cached packet instances for this type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clear() => s_pool.Clear();

    #endregion APIs
}
