// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Framework.DataFrames.Pooling;

/// <summary>
/// Provides a type-specific pool for packet instances with a disposable lease API.
/// </summary>
/// <typeparam name="TPacket">The packet type.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "A generic packet pool is intentionally exposed as a per-packet-type static API.")]
public static class PacketPool<TPacket> where TPacket : PacketBase<TPacket>, new()
{
    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    #endregion Fields

    #region APIs

    /// <summary>
    /// Rents a packet and returns a lease that will automatically return it to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PacketLease<TPacket> Rent() => new(s_pool.Get<TPacket>(), static packet => s_pool.Return(packet));

    /// <summary>
    /// Rents a packet instance directly. Caller must eventually call <see cref="Return"/>.
    /// Prefer <see cref="Rent"/> when possible.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPacket Get() => s_pool.Get<TPacket>();

    /// <summary>
    /// Returns a packet to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(TPacket packet) => s_pool.Return(packet);

    /// <summary>
    /// Preallocates instances for this packet type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Prealloc(int count) => s_pool.Prealloc<TPacket>(count);

    /// <summary>
    /// Clears cached packet instances for this packet type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clear() => s_pool.ClearPool<TPacket>();

    #endregion APIs
}
