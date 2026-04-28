// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Memory.Internal.Buffers;

/// <summary>
/// Orchestrates multiple <see cref="SlabBucket"/> instances — one per configured buffer
/// size class — providing best-fit buffer lookup and unified lifecycle management.
/// </summary>
/// <remarks>
/// This unified manager handles both <see cref="ArraySegment{T}"/> and raw <c>byte[]</c> 
/// requests by utilizing standalone pinned slabs.
/// </remarks>
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SlabPoolManager : IDisposable
{
    /// <summary>Sorted bucket sizes for binary search lookup.</summary>
    private volatile int[] _sortedSizes;

    /// <summary>Buckets keyed by segment size.</summary>
    private readonly Dictionary<int, SlabBucket> _buckets;

    private readonly Lock _lock;
    private volatile int[]? _fastBucketMap; // Fast lookup for sizes up to 4KB
    private bool _disposed;

    /// <summary>Occurs when any bucket managed by this pool manager needs to resize.</summary>
    public event Action<SlabBucket, BufferPoolResizeDirection>? ResizeOccurred;

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
            bucket.ResizeOccurred += (b, d) => this.ResizeOccurred?.Invoke(b, d);
            _buckets[segmentSize] = bucket;
            this.RebuildSortedKeys();
        }
    }


    /// <summary>
    /// Rents a standalone array of at least the requested size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(int size, [NotNullWhen(true)] out byte[]? array)
    {
        int bucketSize = this.FindBestFitSize(size);
        if (bucketSize > 0 && _buckets.TryGetValue(bucketSize, out SlabBucket? bucket))
        {
            array = bucket.Rent();
            return true;
        }

        array = null;
        return false;
    }

    /// <summary>
    /// Returns a raw array to its owning bucket based on array length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReturn(byte[]? array)
    {
        if (array is null)
        {
            return false;
        }

        if (_buckets.TryGetValue(array.Length, out SlabBucket? bucket))
        {
            bucket.Return(array);
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
        if (size <= 0)
        {
            return 0;
        }

        // O(1) Fast path for Abstractions small sizes
        if (size <= 4096)
        {
            return _fastBucketMap?[size] ?? this.BINARY_SEARCH_BEST_FIT(size);
        }

        return this.BINARY_SEARCH_BEST_FIT(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BINARY_SEARCH_BEST_FIT(int size) => BINARY_SEARCH_INTERNAL(_sortedSizes, size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BINARY_SEARCH_INTERNAL(int[] keys, int size)
    {
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

        // Build fast lookup map for sizes 0..4096 using local keys
        // to avoid field read issues during concurrent rebuilds.
        int[] map = new int[4097];
        for (int s = 1; s <= 4096; s++)
        {
            map[s] = BINARY_SEARCH_INTERNAL(keys, s);
        }

        // Atomic assignment — volatile ensures visibility and order
        _sortedSizes = keys;
        _fastBucketMap = map;
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
