// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Framework.Memory.Internal.Buffers;

/// <summary>
/// A large pinned byte array divided into fixed-size segments for high-performance
/// buffer pooling. Each slab is allocated once via <see cref="GC.AllocateArray{T}(int, bool)"/>
/// with <c>pinned: true</c>, so IOCP / native socket operations can use the memory directly
/// without additional pinning.
/// </summary>
/// <remarks>
/// <b>Design rationale:</b>
/// <list type="bullet">
///   <item>One pinning operation per slab instead of per buffer eliminates POH fragmentation.</item>
///   <item>Segments are tracked externally (no inline metadata) so the full segment is clean user bytes.</item>
///   <item>The slab itself is reference-counted: when all segments are returned, the slab
///         can optionally be released to allow the GC to reclaim the memory.</item>
/// </list>
/// </remarks>
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class MemorySlab
{
    /// <summary>The single large pinned backing array for this slab.</summary>
    private readonly byte[] _data;

    /// <summary>Fixed segment size in bytes.</summary>
    private readonly int _segmentSize;

    /// <summary>Total number of segments carved from this slab.</summary>
    private readonly int _segmentCount;

    /// <summary>
    /// Unique slab identifier used for diagnostics and cross-pool validation.
    /// </summary>
    public readonly int SlabId;

    /// <summary>
    /// Number of segments currently rented out (not in the free ring).
    /// When this reaches 0 and the slab is decommissioned, the GC can reclaim the backing array.
    /// </summary>
    private int _activeSegments;

    /// <summary>Global slab ID counter.</summary>
    private static int s_nextSlabId;

    /// <summary>
    /// Initializes a new <see cref="MemorySlab"/> with <paramref name="segmentCount"/> segments
    /// of <paramref name="segmentSize"/> bytes each.
    /// </summary>
    /// <param name="segmentSize">The size of each segment in bytes. Must be positive.</param>
    /// <param name="segmentCount">The number of segments to carve from this slab. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="segmentSize"/> or <paramref name="segmentCount"/> is not positive.</exception>
    public MemorySlab(int segmentSize, int segmentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentCount);

        _segmentSize = segmentSize;
        _segmentCount = segmentCount;
        SlabId = Interlocked.Increment(ref s_nextSlabId);

        // Single pinned allocation — this is the entire point of slab-based pooling.
        // The array lives on the Pinned Object Heap and stays pinned for its lifetime,
        // so IOCP completions can write directly into these segments.
        _data = GC.AllocateArray<byte>(segmentSize * segmentCount, pinned: true);
    }

    /// <summary>Gets the segment size for this slab.</summary>
    public int SegmentSize => _segmentSize;

    /// <summary>Gets the total number of segments in this slab.</summary>
    public int SegmentCount => _segmentCount;

    /// <summary>Gets the number of segments currently in use.</summary>
    public int ActiveSegments => Volatile.Read(ref _activeSegments);

    /// <summary>Gets the total slab size in bytes.</summary>
    public int TotalBytes => _data.Length;

    /// <summary>
    /// Gets whether this slab is fully idle (all segments returned).
    /// </summary>
    public bool IsFullyIdle => Volatile.Read(ref _activeSegments) == 0;

    /// <summary>
    /// Creates an <see cref="ArraySegment{T}"/> for the segment at the given index.
    /// The segment points into the shared slab backing array at the correct offset.
    /// </summary>
    /// <param name="segmentIndex">Zero-based segment index within this slab.</param>
    /// <returns>An <see cref="ArraySegment{T}"/> covering exactly <see cref="SegmentSize"/> bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<byte> GetSegment(int segmentIndex)
    {
        Debug.Assert((uint)segmentIndex < (uint)_segmentCount, "Segment index out of range.");
        int offset = segmentIndex * _segmentSize;
        return new ArraySegment<byte>(_data, offset, _segmentSize);
    }

    /// <summary>
    /// Gets the raw backing array. Callers must use offset arithmetic to locate segments.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] GetBackingArray() => _data;

    /// <summary>
    /// Validates that the given array is the backing array of this slab.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OwnsBacking(byte[] array) => ReferenceEquals(_data, array);

    /// <summary>
    /// Increments the active segment counter. Called when a segment is rented out.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementActive() => Interlocked.Increment(ref _activeSegments);

    /// <summary>
    /// Decrements the active segment counter. Called when a segment is returned.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecrementActive() => Interlocked.Decrement(ref _activeSegments);
}
