// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Framework.Memory.Internal.Buffers;

/// <summary>
/// Orchestrates multiple <see cref="SlabBucket"/> instances — one per configured buffer
/// size class — providing best-fit segment lookup and unified lifecycle management.
/// </summary>
/// <remarks>
/// This is the slab-side counterpart of <see cref="BufferPoolCollection"/>.
/// <see cref="BufferPoolCollection"/> manages per-buffer pinned arrays for the
/// <c>byte[]</c> API (BufferPoolManager.Rent),
/// while <see cref="SlabPoolManager"/> manages slab-backed segments for the
/// <see cref="ArraySegment{T}"/> API (BufferPoolManager.RentSegment).
/// </remarks>
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SlabPoolManager : IDisposable
{
    /// <summary>Sorted bucket sizes for binary search lookup.</summary>
    private int[] _sortedSizes;

    /// <summary>Buckets keyed by segment size.</summary>
    private readonly Dictionary<int, SlabBucket> _buckets;

    private readonly Lock _lock;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="SlabPoolManager"/>.
    /// </summary>
    public SlabPoolManager()
    {
        _sortedSizes = [];
        _buckets = new(8);
        _lock = new();
    }

    /// <summary>
    /// Creates and registers a <see cref="SlabBucket"/> for the given segment size.
    /// No-op if a bucket for this size already exists.
    /// </summary>
    /// <param name="segmentSize">The segment size in bytes.</param>
    /// <param name="initialCapacity">Number of segments to preallocate.</param>
    public void CreateBucket(int segmentSize, int initialCapacity)
    {
        lock (_lock)
        {
            if (_buckets.ContainsKey(segmentSize))
            {
                return;
            }

            SlabBucket bucket = new(segmentSize, initialCapacity);
            _buckets[segmentSize] = bucket;
            this.RebuildSortedKeys();
        }
    }

    /// <summary>
    /// Rents a segment of at least the requested size using best-fit lookup.
    /// </summary>
    /// <param name="size">The minimum segment size required.</param>
    /// <param name="segment">The rented segment, or <c>default</c> if no bucket matches.</param>
    /// <returns><c>true</c> if a segment was rented; <c>false</c> if no suitable bucket exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(int size, out ArraySegment<byte> segment)
    {
        int bucketSize = this.FindBestFitSize(size);
        if (bucketSize > 0 && _buckets.TryGetValue(bucketSize, out SlabBucket? bucket))
        {
            segment = bucket.Rent();
            return true;
        }

        segment = default;
        return false;
    }

    /// <summary>
    /// Returns a segment to its owning bucket based on segment count.
    /// </summary>
    /// <param name="segment">The segment to return.</param>
    /// <returns><c>true</c> if the segment was accepted by a bucket; <c>false</c> otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReturn(ArraySegment<byte> segment)
    {
        if (segment.Array is null)
        {
            return false;
        }

        // Route by Count — each slab segment has Count == bucket.SegmentSize.
        if (_buckets.TryGetValue(segment.Count, out SlabBucket? bucket))
        {
            bucket.Return(segment);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all registered buckets for diagnostics and reporting.
    /// </summary>
    public IReadOnlyCollection<SlabBucket> GetAllBuckets()
    {
        lock (_lock)
        {
            return [.. _buckets.Values];
        }
    }

    /// <summary>
    /// Finds the smallest bucket size that can satisfy the requested size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindBestFitSize(int size)
    {
        int[] keys = _sortedSizes;
        if (keys.Length == 0)
        {
            return 0;
        }

        int index = Array.BinarySearch(keys, size);
        if (index >= 0)
        {
            return keys[index]; // Exact match
        }

        // ~index is the insertion point — first key greater than size
        index = ~index;
        return index < keys.Length ? keys[index] : 0;
    }

    /// <summary>
    /// Rebuilds the sorted size array after a bucket is added.
    /// Must be called under _lock.
    /// </summary>
    private void RebuildSortedKeys()
    {
        int[] keys = new int[_buckets.Count];
        int i = 0;
        foreach (int k in _buckets.Keys)
        {
            keys[i++] = k;
        }

        Array.Sort(keys);
        _sortedSizes = keys;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            foreach (SlabBucket bucket in _buckets.Values)
            {
                bucket.Dispose();
            }

            _buckets.Clear();
            _sortedSizes = [];
            _disposed = true;
        }
    }
}
