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
/// Manages a pool of slab-backed buffer segments for a single buffer size class.
/// Multiple <see cref="MemorySlab"/> instances are allocated on demand and their
/// segments are enqueued into a high-speed ring buffer for rent/return operations.
/// </summary>
/// <remarks>
/// <b>Key design decisions:</b>
/// <list type="bullet">
///   <item>
///     <b>Thread-local cache:</b> Each thread gets a small (up to 8) stack of recently
///     returned segments. This avoids ring contention on the hot path for the most
///     common single-thread-rent-and-return pattern (e.g., IOCP completion threads).
///     Cache misses fall through to the shared ring.
///   </item>
///   <item>
///     <b>Shared ring:</b> A SpinLock-guarded circular buffer identical to the existing
///     <c>BufferRing</c> in <see cref="BufferPoolShared"/>. SpinLock is chosen over
///     <c>Lock</c>/<c>Monitor</c> because hold times are &lt;50ns (one array copy) and
///     contention is already mitigated by the thread-local cache.
///   </item>
///   <item>
///     <b>Slab growth:</b> When the ring is empty, a new <see cref="MemorySlab"/> is
///     allocated automatically. The caller never blocks. Memory budget enforcement
///     is handled at the <see cref="BufferPoolManager"/> level.
///   </item>
/// </list>
/// </remarks>
[DebuggerNonUserCode]
[DebuggerDisplay("SIZE={_segmentSize}, Total={_totalSegments}, Free={_freeRing.Count}")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SlabBucket : IDisposable
{
    #region Constants

    /// <summary>Maximum per-thread cache depth to limit memory overhead.</summary>
    private const int ThreadCacheDepth = 8;

    /// <summary>Default number of segments per slab when growing.</summary>
    private const int DefaultSegmentsPerSlab = 32;

    #endregion Constants

    #region Fields

    private readonly int _segmentSize;
    private readonly Lock _slabLock;
    private readonly List<MemorySlab> _slabs;

    private readonly SlabBucketRing _freeRing;

    // Thread-local cache: small per-thread stack that avoids ring contention.
    // Each thread stores up to ThreadCacheDepth segments in a local array.
    // The cache is shared across all SlabBucket instances to reduce memory
    // overhead. Segments are validated by size on dequeue to prevent cross-bucket
    // contamination.
    [ThreadStatic]
    private static ArraySegment<byte>[]? t_cache;
    [ThreadStatic]
    private static int t_cacheCount;

    private int _totalSegments;
    private int _misses;
    private int _hits;
    private bool _disposed;
    private BufferPoolState _poolInfo;
    private int _isOptimizing;

    #endregion Fields

    #region Properties

    /// <summary>Gets the buffer size this bucket manages.</summary>
    public int SegmentSize => _segmentSize;

    /// <summary>Gets the total number of managed segments (free + in use).</summary>
    public int TotalSegments => Volatile.Read(ref _totalSegments);

    /// <summary>Gets the approximate number of free segments available (excludes thread-local caches).</summary>
    public int FreeSegments => _freeRing.Count;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new <see cref="SlabBucket"/> for segments of the given size.
    /// </summary>
    /// <param name="segmentSize">The fixed segment size in bytes.</param>
    /// <param name="initialCapacity">Number of segments to preallocate. If &lt;= 0, no preallocation is done.</param>
    public SlabBucket(int segmentSize, int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentSize);

        _segmentSize = segmentSize;
        _slabLock = new();
        _slabs = new(4);

        int ringCapacity = initialCapacity <= 0
            ? 4
            : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)initialCapacity);

        _freeRing = new SlabBucketRing(ringCapacity);

        if (initialCapacity > 0)
        {
            this.AllocateSlabAndEnqueue(initialCapacity);
        }
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Attempts to acquire a segment from the thread-local cache or the shared ring.
    /// Returns <c>false</c> if no free segment is available.
    /// </summary>
    /// <param name="segment">The rented segment, or <c>default</c> on failure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(out ArraySegment<byte> segment)
    {
        // Fast path: thread-local cache
        if (t_cacheCount > 0 && t_cache is not null)
        {
            int idx = t_cacheCount - 1;
            ArraySegment<byte> cached = t_cache[idx];

            // Validate segment belongs to our size class (thread-local cache is
            // shared across all SlabBucket instances via [ThreadStatic]).
            if (cached.Array is not null && cached.Count == _segmentSize)
            {
                t_cache[idx] = default;
                t_cacheCount--;
                segment = cached;
                _ = Interlocked.Increment(ref _hits);
                return true;
            }
        }

        // Shared ring
        if (_freeRing.TryDequeue(out segment) && segment.Array is not null)
        {
            _ = Interlocked.Increment(ref _hits);
            return true;
        }

        segment = default;
        return false;
    }

    /// <summary>
    /// Rents a segment, allocating a new slab if the ring is exhausted.
    /// This method never returns a null-backed segment.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when segment allocation fails after slab growth (should not happen).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<byte> Rent()
    {
        if (this.TryRent(out ArraySegment<byte> segment))
        {
            return segment;
        }

        _ = Interlocked.Increment(ref _misses);

        // Growth path: allocate a new slab and enqueue its segments, then dequeue one.
        this.AllocateSlabAndEnqueue(DefaultSegmentsPerSlab);

        if (_freeRing.TryDequeue(out segment) && segment.Array is not null)
        {
            return segment;
        }

        // Should not happen — we just enqueued fresh segments.
        throw new InvalidOperationException(
            $"SlabBucket: failed to dequeue after slab growth for segment size {_segmentSize}.");
    }

    /// <summary>
    /// Returns a segment to the thread-local cache or the shared ring.
    /// </summary>
    /// <param name="segment">The segment to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(ArraySegment<byte> segment)
    {
        if (segment.Array is null)
        {
            return;
        }

        if (segment.Count != _segmentSize)
        {
            // Wrong bucket — silently drop. The segment will be GC'd.
            // This protects against cross-bucket return corruption.
            return;
        }

        // Fast path: thread-local cache
        t_cache ??= new ArraySegment<byte>[ThreadCacheDepth];

        if (t_cacheCount < ThreadCacheDepth)
        {
            t_cache[t_cacheCount++] = segment;
            return;
        }

        // Cache full — drain oldest half to the shared ring to maintain balance
        this.DrainCacheToRing();

        // Now put the current segment in the freshly cleared cache slot
        t_cache[t_cacheCount++] = segment;
    }

    /// <summary>
    /// Increases capacity by allocating additional segments via new slabs.
    /// </summary>
    /// <param name="additionalCapacity">Number of segments to add.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            return;
        }

        if (!this.TryBeginOptimize())
        {
            return;
        }

        try
        {
            this.AllocateSlabAndEnqueue(additionalCapacity);
        }
        finally
        {
            this.EndOptimize();
        }
    }

    /// <summary>
    /// Decreases capacity by dequeuing and dropping free segments.
    /// Segments currently rented out are not affected.
    /// </summary>
    /// <param name="capacityToRemove">Number of segments to release.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0)
        {
            return;
        }

        if (!this.TryBeginOptimize())
        {
            return;
        }

        try
        {
            int removed = 0;
            int target = Math.Min(capacityToRemove, _freeRing.Count);

            for (int i = 0; i < target; i++)
            {
                if (_freeRing.TryDequeue(out _))
                {
                    removed++;
                }
                else
                {
                    break;
                }
            }

            if (removed > 0)
            {
                _ = Interlocked.Add(ref _totalSegments, -removed);
            }
        }
        finally
        {
            this.EndOptimize();
        }
    }

    /// <summary>
    /// Gets a snapshot of the current pool state for diagnostics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferPoolState GetPoolInfo() => this.CreateSnapshot();

    /// <summary>
    /// Gets a reference to a cached pool state snapshot for better performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly BufferPoolState GetPoolInfoRef()
    {
        _poolInfo = this.CreateSnapshot();
        return ref _poolInfo;
    }

    #endregion Public API

    #region Private Helpers

    /// <summary>
    /// Allocates a new slab with the given number of segments and enqueues them into the ring.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AllocateSlabAndEnqueue(int segmentCount)
    {
        if (segmentCount <= 0)
        {
            return;
        }

        MemorySlab slab = new(_segmentSize, segmentCount);

        lock (_slabLock)
        {
            _slabs.Add(slab);
        }

        _freeRing.EnsureCapacity(_freeRing.Count + segmentCount);

        int enqueued = 0;
        for (int i = 0; i < segmentCount; i++)
        {
            ArraySegment<byte> segment = slab.GetSegment(i);
            if (_freeRing.TryEnqueue(segment))
            {
                enqueued++;
            }
        }

        if (enqueued > 0)
        {
            _ = Interlocked.Add(ref _totalSegments, enqueued);
        }
    }

    /// <summary>
    /// Drains the older half of the thread-local cache into the shared ring.
    /// This prevents the thread cache from holding too many segments while
    /// keeping the most recently returned ones close.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DrainCacheToRing()
    {
        if (t_cache is null || t_cacheCount == 0)
        {
            return;
        }

        int drainCount = t_cacheCount / 2;
        if (drainCount == 0)
        {
            drainCount = 1;
        }

        for (int i = 0; i < drainCount; i++)
        {
            ArraySegment<byte> cached = t_cache[i];
            if (cached.Array is not null)
            {
                _ = _freeRing.TryEnqueue(cached);
            }
        }

        // Shift remaining items down
        int remaining = t_cacheCount - drainCount;
        for (int i = 0; i < remaining; i++)
        {
            t_cache[i] = t_cache[drainCount + i];
        }

        // Clear old slots
        for (int i = remaining; i < t_cacheCount; i++)
        {
            t_cache[i] = default;
        }

        t_cacheCount = remaining;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BufferPoolState CreateSnapshot()
    {
        return new BufferPoolState
        {
            FreeBuffers = _freeRing.Count,
            TotalBuffers = Volatile.Read(ref _totalSegments),
            BufferSize = _segmentSize,
            Misses = Volatile.Read(ref _misses),
            Hits = Volatile.Read(ref _hits)
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryBeginOptimize() => Interlocked.CompareExchange(ref _isOptimizing, 1, 0) == 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EndOptimize() => Volatile.Write(ref _isOptimizing, 0);

    #endregion Private Helpers

    #region IDisposable

    /// <summary>
    /// Releases all slab resources. Drains the ring and clears slab references.
    /// Segments still in use by callers will be GC'd when those callers release them.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_slabLock)
        {
            if (_disposed)
            {
                return;
            }

            _ = _freeRing.DrainAll();
            _slabs.Clear();

            _disposed = true;
        }
    }

    #endregion IDisposable

    #region Inner: Ring Buffer

    /// <summary>
    /// SpinLock-guarded ring buffer for free slab segments.
    /// Identical structure to the existing <c>BufferRing</c> in <see cref="BufferPoolShared"/>
    /// to maintain consistency across the codebase.
    /// </summary>
    internal sealed class SlabBucketRing
    {
        private ArraySegment<byte>[] _slots;
        private int _head;
        private int _tail;
        private int _count;
        private SpinLock _lock;

        public int Count => Volatile.Read(ref _count);

        public SlabBucketRing(int capacity)
        {
            _slots = new ArraySegment<byte>[capacity];
            _lock = new SpinLock(enableThreadOwnerTracking: false);
        }

        public bool TryEnqueue(ArraySegment<byte> buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (_count == _slots.Length)
                {
                    return false;
                }

                _slots[_tail] = buffer;
                _tail = (_tail + 1) & (_slots.Length - 1);
                _count++;
                return true;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }

        public bool TryDequeue(out ArraySegment<byte> buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (_count == 0)
                {
                    buffer = default;
                    return false;
                }

                buffer = _slots[_head];
                _slots[_head] = default;
                _head = (_head + 1) & (_slots.Length - 1);
                _count--;
                return true;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }

        public void EnsureCapacity(int targetCapacity)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (targetCapacity <= _slots.Length)
                {
                    return;
                }

                uint newSize = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)targetCapacity);
                ArraySegment<byte>[] newSlots = new ArraySegment<byte>[newSize];

                for (int i = 0; i < _count; ++i)
                {
                    newSlots[i] = _slots[(_head + i) & (_slots.Length - 1)];
                }

                _slots = newSlots;
                _head = 0;
                _tail = _count;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }

        [SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
        public ArraySegment<byte>[] DrainAll()
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (_count == 0)
                {
                    return Array.Empty<ArraySegment<byte>>();
                }

                ArraySegment<byte>[] result = new ArraySegment<byte>[_count];
                for (int i = 0; i < _count; ++i)
                {
                    int index = (_head + i) & (_slots.Length - 1);
                    result[i] = _slots[index];
                    _slots[index] = default;
                }

                _head = 0;
                _tail = 0;
                _count = 0;

                return result;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }
    }

    #endregion Inner: Ring Buffer
}
