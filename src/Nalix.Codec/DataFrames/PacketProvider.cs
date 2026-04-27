// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Abstractions;

namespace Nalix.Codec.DataFrames;

/// <summary>
/// Provides an optimized initialization and reclamation mechanism for Packets.
/// Automatically coordinates between renting from a pool (if configured) or creating via constructor.
/// </summary>
/// <typeparam name="T">The specific Packet type, must inherit from <see cref="PacketBase{T}"/>.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
public static class PacketProvider<T> where T : PacketBase<T>, new()
{
    /// <summary>
    /// Creates or rents an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <returns>A packet instance ready for use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Create()
    {
        // Use a local variable to optimize static field access
        IObjectPoolManager? mgr = PacketRegistry.Manager;
        return mgr == null ? new T() : mgr.Get<T>();
    }

    /// <summary>
    /// Reclaims the packet and returns it to the pool if pooling is enabled.
    /// </summary>
    /// <param name="packet">The packet instance to reclaim.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T packet)
    {
        IObjectPoolManager? mgr = PacketRegistry.Manager;
        mgr?.Return(packet);
    }
}

