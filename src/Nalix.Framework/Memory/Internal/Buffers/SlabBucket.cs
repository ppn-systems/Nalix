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
/// Manages a pool of standalone pinned byte arrays for a single buffer size class.
/// Each buffer is an independent <see cref="MemorySlab"/> allocated on the Pinned Object Heap.
/// This ensures that the public <c>byte[]</c> API always delivers arrays where data starts
/// at offset 0, while maintaining the diagnostic and metrics benefits of slab management.
/// </summary>
/// <remarks>
/// <b>Key design decisions:</b>
/// <list type="bullet">
///   <item>
///     <b>Thread-local cache:</b> Each thread gets a small stack of recently returned buffers
///     to avoid ring contention on the hot path.
///   </item>
///   <item>
///     <b>Standalone Slabs:</b> Multi-segment slicing is removed in favor of individual pinned
///     arrays. This eliminates complex offset management and ensures 100% compatibility with
///     legacy APIs that expect data at <c>index 0</c>.
///   </item>
/// </list>
/// </remarks>
[DebuggerNonUserCode]
[DebuggerDisplay("SIZE={_segmentSize}, Total={_totalBuffers}, Free={_freeRing.Count}")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SlabBucket : IDisposable
{
    #region Fields

    private readonly int _segmentSize;
    private readonly int _initialCapacity;
    private readonly int _cacheDepth;
    private readonly Lock _slabLock;
    private readonly SlabBucketRing _freeRing;
    private readonly Dictionary<byte[], MemorySlab> _slabLookup = new();
    private readonly ThreadLocal<ThreadLocalCache> _threadCache;

    private int _totalBuffers;
    private int _rentedCount;
    private int _misses;
    private int _hits;
    private int _expands;
    private int _shrinks;
    private bool _disposed;
    private int _isOptimizing;

    /// <summary>Occurs when the bucket needs to resize (expand or shrink).</summary>
    public event Action<SlabBucket, BufferPoolResizeDirection>? ResizeOccurred;

    private sealed class ThreadLocalCache
    {
        public readonly byte[]?[] Cache;
        public int Count;

        public ThreadLocalCache(int depth) => Cache = new byte[depth][];
    }

    #endregion Fields

    #region Properties

    /// <summary>Gets the buffer size this bucket manages.</summary>
    public int SegmentSize => _segmentSize;

    /// <summary>Gets the total number of managed buffers (free + in use).</summary>
    public int TotalBuffers => Volatile.Read(ref _totalBuffers);

    /// <summary>Gets the approximate number of free buffers available.</summary>
    public int FreeBuffers => Math.Max(0, Volatile.Read(ref _totalBuffers) - Volatile.Read(ref _rentedCount));

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new <see cref="SlabBucket"/> for standalone arrays of the given size.
    /// </summary>
    /// <param name="segmentSize">The buffer size in bytes.</param>
    /// <param name="initialCapacity">Number of buffers to preallocate.</param>
    /// <param name="cacheDepth">Maximum depth for the per-thread cache.</param>
    public SlabBucket(int segmentSize, int initialCapacity, int cacheDepth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cacheDepth);

        _segmentSize = segmentSize;
        _initialCapacity = initialCapacity;
        _cacheDepth = cacheDepth;
        _slabLock = new();

        int ringCapacity = initialCapacity <= 0
            ? 4
            : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)initialCapacity);

        _freeRing = new SlabBucketRing(ringCapacity);
        _threadCache = new ThreadLocal<ThreadLocalCache>(() => new ThreadLocalCache(cacheDepth), trackAllValues: false);

        if (initialCapacity > 0)
        {
            this.AllocateAndEnqueue(initialCapacity);
        }
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Attempts to acquire a buffer from the thread-local cache or the shared ring.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent([NotNullWhen(true)] out byte[]? array)
    {
        ThreadLocalCache cache = _threadCache.Value!;
        if (cache.Count > 0)
        {
            int idx = --cache.Count;
            byte[]? cached = cache.Cache[idx];
            cache.Cache[idx] = null;

            _ = Interlocked.Increment(ref _hits);
            _ = Interlocked.Increment(ref _rentedCount);
            array = cached!;
            return true;
        }

        if (_freeRing.TryDequeue(out array))
        {
            _ = Interlocked.Increment(ref _hits);
            _ = Interlocked.Increment(ref _rentedCount);
            return true;
        }

        array = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Rent()
    {
        if (this.TryRent(out byte[]? array))
        {
            return array;
        }

        _ = Interlocked.Increment(ref _misses);

        // Notify manager that we need to grow.
        this.ResizeOccurred?.Invoke(this, BufferPoolResizeDirection.Increase);

        if (this.TryRent(out array))
        {
            return array;
        }

        // Emergency fallback: if manager rejected growth, allocate one anyway 
        // to prevent consumer failure, but this should be rare.
        this.AllocateAndEnqueue(1);

        if (this.TryRent(out array))
        {
            return array;
        }

        throw new InvalidOperationException($"SlabBucket: failed to allocate standalone buffer of size {_segmentSize}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(byte[] array)
    {
        if (array is null || array.Length != _segmentSize)
        {
            return;
        }

        _ = Interlocked.Decrement(ref _rentedCount);

        ThreadLocalCache cache = _threadCache.Value!;
        if (cache.Count < _cacheDepth)
        {
            cache.Cache[cache.Count++] = array;
            return;
        }

        this.DrainCacheToRing(cache);
        cache.Cache[cache.Count++] = array;
    }

    /// <summary>Increases capacity by adding more standalone arrays.</summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0 || !this.TryBeginOptimize())
        {
            return;
        }

        try
        {
            this.AllocateAndEnqueue(additionalCapacity);
            _ = Interlocked.Increment(ref _expands);
        }
        finally
        {
            this.EndOptimize();
        }
    }

    /// <summary>Decreases capacity by dropping free buffers and releasing slabs.</summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0 || !this.TryBeginOptimize())
        {
            return;
        }

        try
        {
            // NEW RULE: Do not shrink below the initial (default) capacity
            int currentTotal = Volatile.Read(ref _totalBuffers);
            int canRemove = Math.Min(capacityToRemove, currentTotal - _initialCapacity);
            if (canRemove <= 0)
            {
                return;
            }

            int removed = 0;
            int ringCount = _freeRing.Count;
            int target = Math.Min(canRemove, ringCount);

            for (int i = 0; i < target; i++)
            {
                if (_freeRing.TryDequeue(out byte[]? array))
                {
                    lock (_slabLock)
                    {
                        if (array != null && _slabLookup.Remove(array))
                        {
                            removed++;
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            if (removed > 0)
            {
                _ = Interlocked.Add(ref _totalBuffers, -removed);
                _ = Interlocked.Increment(ref _shrinks);
            }
        }
        finally
        {
            this.EndOptimize();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferPoolState GetPoolInfo() => this.CreateSnapshot();

    #endregion Public API

    #region Private Helpers

    /// <summary>
    /// Allocates individual pinned arrays and enqueues them.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AllocateAndEnqueue(int count)
    {
        if (count <= 0)
        {
            return;
        }

        _freeRing.EnsureCapacity(_freeRing.Count + count);

        int enqueued = 0;
        lock (_slabLock)
        {
            for (int i = 0; i < count; i++)
            {
                MemorySlab slab = new(_segmentSize);
                byte[] array = slab.GetArray();
                _slabLookup[array] = slab;

                if (_freeRing.TryEnqueue(array))
                {
                    enqueued++;
                }
            }
        }

        if (enqueued > 0)
        {
            _ = Interlocked.Add(ref _totalBuffers, enqueued);
        }
    }

    /// <summary>Drains the current thread's cache to the shared ring.</summary>
    private void DrainCacheToRing(ThreadLocalCache cache)
    {
        int toMove = cache.Count / 2;

        // Ensure the ring can hold the new buffers to prevent loss.
        _freeRing.EnsureCapacity(_freeRing.Count + toMove);

        for (int i = 0; i < toMove; i++)
        {
            byte[]? arr = cache.Cache[i];
            if (arr != null)
            {
                _ = _freeRing.TryEnqueue(arr);
            }
        }

        // Shift remaining
        int remaining = cache.Count - toMove;
        Array.Copy(cache.Cache, toMove, cache.Cache, 0, remaining);
        Array.Clear(cache.Cache, remaining, toMove);
        cache.Count = remaining;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BufferPoolState CreateSnapshot()
    {
        int total = Volatile.Read(ref _totalBuffers);
        int rented = Volatile.Read(ref _rentedCount);

        return new BufferPoolState
        {
            FreeBuffers = Math.Max(0, total - rented),
            TotalBuffers = total,
            InitialCapacity = _initialCapacity,
            Expands = Volatile.Read(ref _expands),
            Shrinks = Volatile.Read(ref _shrinks),
            BufferSize = _segmentSize,
            Misses = Volatile.Read(ref _misses),
            Hits = Volatile.Read(ref _hits)
        };
    }

    private bool TryBeginOptimize() => Interlocked.CompareExchange(ref _isOptimizing, 1, 0) == 0;
    private void EndOptimize() => Volatile.Write(ref _isOptimizing, 0);

    #endregion Private Helpers

    #region IDisposable

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
            _slabLookup.Clear();
            _threadCache.Dispose();
            _disposed = true;
        }
    }

    #endregion IDisposable

    #region Inner: Ring Buffer

    internal sealed class SlabBucketRing
    {
        private byte[]?[] _slots;
        private int _head;
        private int _tail;
        private int _count;
        private SpinLock _lock;

        public int Count => Volatile.Read(ref _count);

        public SlabBucketRing(int capacity)
        {
            _slots = new byte[capacity][];
            _lock = new SpinLock(false);
        }

        public bool TryEnqueue(byte[] buffer)
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

        public bool TryDequeue([NotNullWhen(true)] out byte[]? buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == 0) { buffer = null; return false; }
                buffer = _slots[_head]!;
                _slots[_head] = null;
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
                byte[][] newSlots = new byte[newSize][];
                for (int i = 0; i < _count; ++i)
                {
                    newSlots[i] = _slots![(_head + i) & (_slots.Length - 1)]!;
                }

                _slots = newSlots; _head = 0; _tail = _count;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }

        public byte[][] DrainAll()
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == 0)
                {
                    return Array.Empty<byte[]>();
                }

                byte[][] result = new byte[_count][];
                for (int i = 0; i < _count; ++i)
                {
                    int index = (_head + i) & (_slots.Length - 1);
                    result[i] = _slots[index]!;
                    _slots[index] = null;
                }
                _head = _tail = _count = 0;
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

