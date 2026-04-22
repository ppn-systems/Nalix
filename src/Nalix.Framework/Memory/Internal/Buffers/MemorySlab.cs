// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Framework.Memory.Internal.Buffers;

/// <summary>
/// A standalone pinned byte array for high-performance buffer pooling.
/// Each slab is allocated once via <see cref="GC.AllocateArray{T}(int, bool)"/>
/// with <c>pinned: true</c>, so IOCP / native socket operations can use the memory directly
/// without additional pinning.
/// </summary>
/// <remarks>
/// <b>Design rationale:</b>
/// <list type="bullet">
///   <item>Standalone pinned arrays eliminate complex slicing logic and ensure data always starts at offset 0.</item>
///   <item>Pinned memory prevents GC fragmentation for long-lived network buffers.</item>
/// </list>
/// </remarks>
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class MemorySlab
{
    /// <summary>The single pinned backing array for this slab.</summary>
    private readonly byte[] _data;

    /// <summary>
    /// Unique slab identifier used for diagnostics.
    /// </summary>
    public readonly int SlabId;

    /// <summary>
    /// Whether this slab is currently rented out.
    /// </summary>
    private int _isActive;

    /// <summary>Global slab ID counter.</summary>
    private static int s_nextSlabId;

    /// <summary>
    /// Initializes a new <see cref="MemorySlab"/> with a pinned array of the given size.
    /// </summary>
    /// <param name="size">The size of the array in bytes.</param>
    public MemorySlab(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        SlabId = Interlocked.Increment(ref s_nextSlabId);

        // Single pinned allocation — the entire point of standalone slab-based pooling.
        // The array lives on the Pinned Object Heap and stays pinned for its lifetime.
        _data = GC.AllocateArray<byte>(size, pinned: true);
    }

    /// <summary>Gets the total slab size in bytes.</summary>
    public int TotalBytes => _data.Length;

    /// <summary>
    /// Gets whether this slab is currently idle.
    /// </summary>
    public bool IsIdle => Volatile.Read(ref _isActive) == 0;

    /// <summary>
    /// Gets the raw backing array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] GetArray() => _data;

    /// <summary>
    /// Validates that the given array is the backing array of this slab.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OwnsBacking(byte[] array) => ReferenceEquals(_data, array);

    /// <summary>
    /// Marks the slab as rented.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkActive() => Volatile.Write(ref _isActive, 1);

    /// <summary>
    /// Marks the slab as idle.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkIdle() => Volatile.Write(ref _isActive, 0);
}
