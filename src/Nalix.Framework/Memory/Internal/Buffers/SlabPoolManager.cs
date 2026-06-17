// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
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
    /// <summary>Buckets sorted by size for binary search lookup.</summary>
    private volatile SlabBucket[] _sortedBuckets;

    /// <summary>Buckets keyed by segment size.</summary>
    private readonly Dictionary<int, SlabBucket> _buckets;

    private readonly Lock _lock;

    /// <summary>Fast lookup for sizes up to 4KB with 16-byte granularity (indices 0..256).</summary>
    private volatile SlabBucket?[]? _fastBucketMap;

    private bool _disposed;

    /// <summary>Occurs when any bucket managed by this pool manager needs to resize.</summary>
    public event Action<SlabBucket, BufferPoolResizeDirection>? ResizeOccurred;

    /// <summary>
    /// Initializes a new <see cref="SlabPoolManager"/>.
    /// </summary>
    public SlabPoolManager()
    {
        _sortedBuckets = Array.Empty<SlabBucket>();
        _buckets = new(8);
        _lock = new();
    }

    /// <summary>
    /// Creates and registers a <see cref="SlabBucket"/> for the given segment size.
    /// No-op if a bucket for this size already exists.
    /// </summary>
    /// <param name="segmentSize">The segment size in bytes.</param>
    /// <param name="initialCapacity">Number of segments to preallocate.</param>
    /// <param name="cacheDepth">The thread-local cache depth.</param>
    /// <param name="returnValidation">Rented-address validation mode for the bucket.</param>
    public void CreateBucket(int segmentSize, int initialCapacity, int cacheDepth = 8,
                             ReturnValidation returnValidation = ReturnValidation.Disabled)
    {
        lock (_lock)
        {
            if (_buckets.ContainsKey(segmentSize))
            {
                return;
            }

            SlabBucket bucket = new(segmentSize, initialCapacity, cacheDepth, returnValidation);
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
        SlabBucket? bucket = this.FindBucket(size);
        if (bucket != null)
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

        SlabBucket? bucket = this.FindExactBucket(array.Length);
        if (bucket != null)
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
    /// Finds the smallest bucket that can satisfy the requested size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SlabBucket? FindBucket(int size)
    {
        if (size <= 0)
        {
            return null;
        }

        SlabBucket?[]? map = _fastBucketMap;
        if (size <= 4096 && map != null)
        {
            int index = (size + 15) >> 4;
            return map[index];
        }

        return this.BinarySearchBestFit(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SlabBucket? BinarySearchBestFit(int size)
    {
        SlabBucket[] buckets = _sortedBuckets;
        if (buckets.Length == 0)
        {
            return null;
        }

        int low = 0;
        int high = buckets.Length - 1;
        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            int midSize = buckets[mid].SegmentSize;
            if (midSize == size)
            {
                return buckets[mid];
            }
            if (midSize < size)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low < buckets.Length ? buckets[low] : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SlabBucket? FindExactBucket(int size)
    {
        SlabBucket?[]? map = _fastBucketMap;
        if (size <= 4096 && map != null)
        {
            int index = (size + 15) >> 4;
            SlabBucket? bucket = map[index];
            if (bucket != null && bucket.SegmentSize == size)
            {
                return bucket;
            }
            return null;
        }

        SlabBucket[] buckets = _sortedBuckets;
        int low = 0;
        int high = buckets.Length - 1;
        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            int midSize = buckets[mid].SegmentSize;
            if (midSize == size)
            {
                return buckets[mid];
            }
            if (midSize < size)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return null;
    }

    /// <summary>
    /// Rebuilds the sorted size array after a bucket is added.
    /// Must be called under _lock.
    /// </summary>
    private void RebuildSortedKeys()
    {
        SlabBucket[] sorted = new SlabBucket[_buckets.Count];
        int i = 0;
        foreach (SlabBucket b in _buckets.Values)
        {
            sorted[i++] = b;
        }

        Array.Sort(sorted, static (a, b) => a.SegmentSize.CompareTo(b.SegmentSize));

        // Build fast lookup map for sizes 0..4096 with 16-byte granularity (indices 0..256)
        SlabBucket?[] map = new SlabBucket?[257];
        for (int idx = 0; idx <= 256; idx++)
        {
            int targetSize = idx << 4;
            if (targetSize == 0)
            {
                continue;
            }

            // Find the best fit for this targetSize
            SlabBucket? bestFit = null;
            foreach (SlabBucket b in sorted)
            {
                if (b.SegmentSize >= targetSize)
                {
                    bestFit = b;
                    break;
                }
            }
            map[idx] = bestFit;
        }

        // Atomic assignment — volatile ensures visibility and order
        _sortedBuckets = sorted;
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
            _sortedBuckets = Array.Empty<SlabBucket>();
            _fastBucketMap = null;
            _disposed = true;
        }
    }
}
