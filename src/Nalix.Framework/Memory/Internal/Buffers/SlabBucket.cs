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
    #region Constants

    /// <summary>Maximum per-thread cache depth to limit memory overhead.</summary>
    private const int ThreadCacheDepth = 8;

    #endregion Constants

    #region Fields

    private readonly int _segmentSize;
    private readonly Lock _slabLock;
    private readonly List<MemorySlab> _slabs;

    private readonly SlabBucketRing _freeRing;

    [ThreadStatic]
    private static ArraySegment<byte>[]? t_cache;
    [ThreadStatic]
    private static int t_cacheCount;

    private int _totalBuffers;
    private int _misses;
    private int _hits;
    private bool _disposed;
    private BufferPoolState _poolInfo;
    private int _isOptimizing;

    #endregion Fields

    #region Properties

    /// <summary>Gets the buffer size this bucket manages.</summary>
    public int SegmentSize => _segmentSize;

    /// <summary>Gets the total number of managed buffers (free + in use).</summary>
    public int TotalBuffers => Volatile.Read(ref _totalBuffers);

    /// <summary>Gets the approximate number of free buffers available.</summary>
    public int FreeBuffers => _freeRing.Count;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new <see cref="SlabBucket"/> for standalone arrays of the given size.
    /// </summary>
    /// <param name="segmentSize">The buffer size in bytes.</param>
    /// <param name="initialCapacity">Number of buffers to preallocate.</param>
    public SlabBucket(int segmentSize, int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentSize);

        _segmentSize = segmentSize;
        _slabLock = new();
        _slabs = new(initialCapacity > 0 ? initialCapacity : 4);

        int ringCapacity = initialCapacity <= 0
            ? 4
            : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)initialCapacity);

        _freeRing = new SlabBucketRing(ringCapacity);

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
    public bool TryRent(out ArraySegment<byte> segment)
    {
        if (t_cacheCount > 0 && t_cache is not null)
        {
            int idx = t_cacheCount - 1;
            ArraySegment<byte> cached = t_cache[idx];

            if (cached.Array is not null && cached.Count == _segmentSize)
            {
                t_cache[idx] = default;
                t_cacheCount--;
                segment = cached;
                _ = Interlocked.Increment(ref _hits);
                return true;
            }
        }

        if (_freeRing.TryDequeue(out segment) && segment.Array is not null)
        {
            _ = Interlocked.Increment(ref _hits);
            return true;
        }

        segment = default;
        return false;
    }

    /// <summary>
    /// Rents a buffer as an <see cref="ArraySegment{T}"/> with offset 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<byte> Rent()
    {
        if (this.TryRent(out ArraySegment<byte> segment))
        {
            return segment;
        }

        _ = Interlocked.Increment(ref _misses);

        // Individual allocation for standalone slabs.
        this.AllocateAndEnqueue(1);

        if (_freeRing.TryDequeue(out segment) && segment.Array is not null)
        {
            return segment;
        }

        throw new InvalidOperationException($"SlabBucket: failed to allocate standalone buffer of size {_segmentSize}.");
    }

    /// <summary>
    /// Rents a raw <c>byte[]</c> (always offset 0).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] RentArray() => this.Rent().Array!;

    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(ArraySegment<byte> segment)
    {
        if (segment.Array is null || segment.Count != _segmentSize)
        {
            return;
        }

        t_cache ??= new ArraySegment<byte>[ThreadCacheDepth];

        if (t_cacheCount < ThreadCacheDepth)
        {
            t_cache[t_cacheCount++] = segment;
            return;
        }

        this.DrainCacheToRing();
        t_cache[t_cacheCount++] = segment;
    }

    /// <summary>
    /// Returns a raw <c>byte[]</c> to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(byte[] array)
    {
        if (array == null) return;
        this.Return(new ArraySegment<byte>(array, 0, array.Length));
    }

    /// <summary>Increases capacity by adding more standalone arrays.</summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0 || !this.TryBeginOptimize()) return;

        try
        {
            this.AllocateAndEnqueue(additionalCapacity);
        }
        finally
        {
            this.EndOptimize();
        }
    }

    /// <summary>Decreases capacity by dropping free buffers.</summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0 || !this.TryBeginOptimize()) return;

        try
        {
            int removed = 0;
            int target = Math.Min(capacityToRemove, _freeRing.Count);

            for (int i = 0; i < target; i++)
            {
                if (_freeRing.TryDequeue(out _)) removed++;
                else break;
            }

            if (removed > 0) _ = Interlocked.Add(ref _totalBuffers, -removed);
        }
        finally
        {
            this.EndOptimize();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferPoolState GetPoolInfo() => this.CreateSnapshot();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly BufferPoolState GetPoolInfoRef()
    {
        _poolInfo = this.CreateSnapshot();
        return ref _poolInfo;
    }

    #endregion Public API

    #region Private Helpers

    /// <summary>
    /// Allocates individual pinned arrays and enqueues them.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AllocateAndEnqueue(int count)
    {
        if (count <= 0) return;

        _freeRing.EnsureCapacity(_freeRing.Count + count);

        int enqueued = 0;
        lock (_slabLock)
        {
            for (int i = 0; i < count; i++)
            {
                MemorySlab slab = new(_segmentSize);
                _slabs.Add(slab);
                
                if (_freeRing.TryEnqueue(new ArraySegment<byte>(slab.GetArray(), 0, _segmentSize)))
                {
                    enqueued++;
                }
            }
        }

        if (enqueued > 0) _ = Interlocked.Add(ref _totalBuffers, enqueued);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DrainCacheToRing()
    {
        if (t_cache is null || t_cacheCount == 0) return;

        int drainCount = Math.Max(1, t_cacheCount / 2);
        for (int i = 0; i < drainCount; i++)
        {
            ArraySegment<byte> cached = t_cache[i];
            if (cached.Array is not null) _ = _freeRing.TryEnqueue(cached);
        }

        int remaining = t_cacheCount - drainCount;
        for (int i = 0; i < remaining; i++) t_cache[i] = t_cache[drainCount + i];
        for (int i = remaining; i < t_cacheCount; i++) t_cache[i] = default;
        t_cacheCount = remaining;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BufferPoolState CreateSnapshot()
    {
        return new BufferPoolState
        {
            FreeBuffers = _freeRing.Count,
            TotalBuffers = Volatile.Read(ref _totalBuffers),
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
        if (_disposed) return;
        lock (_slabLock)
        {
            if (_disposed) return;
            _ = _freeRing.DrainAll();
            _slabs.Clear();
            _disposed = true;
        }
    }

    #endregion IDisposable

    #region Inner: Ring Buffer

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
            _lock = new SpinLock(false);
        }

        public bool TryEnqueue(ArraySegment<byte> buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == _slots.Length) return false;
                _slots[_tail] = buffer;
                _tail = (_tail + 1) & (_slots.Length - 1);
                _count++;
                return true;
            }
            finally { if (taken) _lock.Exit(); }
        }

        public bool TryDequeue(out ArraySegment<byte> buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == 0) { buffer = default; return false; }
                buffer = _slots[_head];
                _slots[_head] = default;
                _head = (_head + 1) & (_slots.Length - 1);
                _count--;
                return true;
            }
            finally { if (taken) _lock.Exit(); }
        }

        public void EnsureCapacity(int targetCapacity)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (targetCapacity <= _slots.Length) return;
                uint newSize = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)targetCapacity);
                ArraySegment<byte>[] newSlots = new ArraySegment<byte>[newSize];
                for (int i = 0; i < _count; ++i)
                    newSlots[i] = _slots[(_head + i) & (_slots.Length - 1)];
                _slots = newSlots; _head = 0; _tail = _count;
            }
            finally { if (taken) _lock.Exit(); }
        }

        public ArraySegment<byte>[] DrainAll()
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == 0) return Array.Empty<ArraySegment<byte>>();
                ArraySegment<byte>[] result = new ArraySegment<byte>[_count];
                for (int i = 0; i < _count; ++i)
                {
                    int index = (_head + i) & (_slots.Length - 1);
                    result[i] = _slots[index];
                    _slots[index] = default;
                }
                _head = _tail = _count = 0;
                return result;
            }
            finally { if (taken) _lock.Exit(); }
        }
    }

    #endregion Inner: Ring Buffer
}

