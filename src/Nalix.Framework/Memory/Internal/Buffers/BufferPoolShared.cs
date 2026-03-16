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
/// Manages a pool of shared buffers with optimized memory handling.
/// </summary>
[DebuggerNonUserCode]
[DebuggerDisplay("SIZE={_bufferSize}, Total={_totalBuffers}, Free={_freeBuffers.Count}")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class BufferPoolShared : IDisposable
{
    #region Fields

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, BufferPoolShared> s_pools = new();

    private readonly BufferRing _freeBuffers;
    private readonly int _bufferSize;
    private readonly Lock _disposeLock;
    private readonly System.Buffers.ArrayPool<byte> _arrayPool;

    private int _misses;
    private int _hits;
    private bool _disposed;
    private BufferPoolState _poolInfo;
    private int _totalBuffers;
    private int _isOptimizing;

    #endregion Fields

    #region Properties

    /// <summary>
    /// The total number of buffers in the pool.
    /// </summary>
    public int TotalBuffers => Volatile.Read(ref _totalBuffers);

    /// <summary>
    /// The number of free buffers in the pool.
    /// </summary>
    public int FreeBuffers => _freeBuffers.Count;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolShared"/> class.
    /// </summary>
    /// <param name="bufferSize">The size of buffers managed by the pool.</param>
    /// <param name="initialCapacity">The number of buffers to preallocate.</param>
    private BufferPoolShared(int bufferSize, int initialCapacity)
    {
        _disposeLock = new();
        _bufferSize = bufferSize;
        _arrayPool = System.Buffers.ArrayPool<byte>.Shared;

        int ringCapacity = initialCapacity <= 0
            ? 4
            : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)initialCapacity);

        _freeBuffers = new BufferRing(ringCapacity);

        this.PreallocateBuffers(initialCapacity);
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Gets or creates a shared buffer pool for the specified buffer size.
    /// </summary>
    /// <param name="bufferSize">The size of buffers managed by the pool.</param>
    /// <param name="initialCapacity">The number of buffers to preallocate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferPoolShared GetOrCreatePool(int bufferSize, int initialCapacity)
        => s_pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity));

    /// <summary>
    /// Attempts to acquire a buffer from the managed ring without falling back 
    /// to the shared ArrayPool. Returns true if a buffer was successfully dequeued.
    /// </summary>
    /// <param name="buffer">The acquired buffer, or null if empty.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAcquireBuffer(out ArraySegment<byte> buffer)
    {
        if (_freeBuffers.TryDequeue(out buffer) && buffer.Array is not null)
        {
            _ = Interlocked.Increment(ref _hits);
            return true;
        }

        buffer = default;
        return false;
    }

    /// <summary>
    /// Acquires a buffer from the pool with fast path optimization.
    /// Falls back to the shared ArrayPool if the managed ring is empty.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<byte> AcquireBuffer()
    {
        if (this.TryAcquireBuffer(out ArraySegment<byte> buffer))
        {
            return buffer;
        }

        _ = Interlocked.Increment(ref _misses);

        // Fallback to shared ArrayPool. We count these in _totalBuffers to maintain
        // accurate 'In Use' metrics during the lease period.
        byte[] newBuffer = _arrayPool.Rent(_bufferSize);
        _ = Interlocked.Increment(ref _totalBuffers);

        return new ArraySegment<byte>(newBuffer, 0, _bufferSize);
    }

    /// <summary>
    /// Releases a buffer back into the pool.
    /// </summary>
    /// <param name="buffer">The buffer to return to the pool.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="buffer"/> is too small for this pool.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseBuffer(ArraySegment<byte> buffer)
    {
        if (buffer.Array is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (buffer.Count < _bufferSize)
        {
            throw new ArgumentException(
                $"Buffer too small: count={buffer.Count}, expected at least {_bufferSize}.");
        }

        // We only retain buffers that match our exact pool size in the high-speed ring.
        // This ensures strict batch uniformity as intended by the manager's design.
        if (buffer.Count == _bufferSize && _freeBuffers.TryEnqueue(buffer))
        {
            return;
        }

        // Mismatched size or full ring: return to shared pool and decrease tracking count.
        // If the segment comes from a pinned slab, the underlying ArrayPool.Return will ignore it
        // if it wasn't rented from the shared pool, or we just drop it.
        // Wait, ArrayPool.Return on a pinned slab array will throw or corrupt the tool!
        // Actually, we must ONLY return to ArrayPool if it came from ArrayPool.
        // How do we know? If it was fallback, we return it to ArrayPool.
        // If it was from a slab, we just drop the segment (it's lost from the ring, but GC safe).
        // Since we only return buffers of EXACT size, fallback buffers (which are usually larger, e.g. 512 for 256)
        // will safely hit this path. Pinned slab segments are EXACTLY _bufferSize.
        // But what if a pinned slab segment fails TryEnqueue because the ring is full?
        // Then we MUST NOT pass the pinned slab array to ArrayPool.Return!
        // For now, we just decrement the counter and let it be collected.
        
        // _arrayPool.Return(buffer.Array); // DANGEROUS for slabs! Removed.
        _ = Interlocked.Decrement(ref _totalBuffers);
    }

    /// <summary>
    /// Increases the capacity of the pool by adding buffers.
    /// </summary>
    /// <param name="additionalCapacity">The number of additional buffers to add.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="additionalCapacity"/> is less than or equal to zero.</exception>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            throw new ArgumentException("The additional quantity must be greater than zero.");
        }

        if (!this.TryBeginOptimize())
        {
            return;
        }

        try
        {
            this.RentAndEnqueueBuffers(additionalCapacity);
        }
        finally
        {
            this.EndOptimize();
        }
    }

    /// <summary>
    /// Decreases the capacity of the pool by removing buffers.
    /// </summary>
    /// <param name="capacityToRemove">The number of buffers to remove.</param>
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
            int target = Math.Min(capacityToRemove, _freeBuffers.Count);

            for (int i = 0; i < target; i++)
            {
                if (_freeBuffers.TryDequeue(out ArraySegment<byte> buf))
                {
                    // We just drop the reference here instead of trying to return it to ArrayPool.
                    // This allows the GC to clean up the pinned slab once all segments are freed.
                    removed++;
                }
                else
                {
                    break;
                }
            }

            if (removed > 0)
            {
                _ = Interlocked.Add(ref _totalBuffers, -removed);
            }
        }
        finally
        {
            this.EndOptimize();
        }
    }

    /// <summary>
    /// Gets information about the buffer pool by value (cheap snapshot).
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferPoolState GetPoolInfo() => this.CreatePoolStateSnapshot();

    /// <summary>
    /// Gets information about the buffer pool by reference for better performance.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly BufferPoolState GetPoolInfoRef()
    {
        _poolInfo = this.CreatePoolStateSnapshot();
        return ref _poolInfo;
    }

    #endregion Public API

    #region IDisposable

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for cleanup when Dispose is not called.
    /// </summary>
    ~BufferPoolShared()
    {
        this.Dispose(false);
    }

    #endregion IDisposable

    #region Private Helpers

    /// <summary>
    /// Pre-allocates buffers to the specified capacity.
    /// </summary>
    /// <param name="capacity">The number of buffers to preallocate.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PreallocateBuffers(int capacity)
    {
        if (capacity <= 0)
        {
            return;
        }

        _freeBuffers.EnsureCapacity(capacity);
        this.RentAndEnqueueBuffers(capacity);
    }

    /// <summary>
    /// Rents buffers from the ArrayPool and enqueues them into the ring.
    /// </summary>
    /// <param name="count">The number of buffers to rent and enqueue.</param>
    [StackTraceHidden]
    private void RentAndEnqueueBuffers(int count)
    {
        if (count <= 0)
        {
            return;
        }

        int actualEnqueued = 0;

        for (int i = 0; i < count; ++i)
        {
            // Per-buffer Pinned Object Heap allocation.
            // Each buffer is an independent pinned array: array.Length == _bufferSize.
            // This guarantees that the public byte[] API always delivers arrays where
            // data starts at offset 0 — required by FrameReader, FrameSender, and all
            // network components that write directly to array[0..].
            // Batch-slab (one large array sliced into segments) cannot satisfy this
            // contract without non-zero segment offsets, which break the entire stack.
            byte[] buf = GC.AllocateArray<byte>(_bufferSize, pinned: true);
            ArraySegment<byte> segment = new(buf, 0, _bufferSize);

            if (_freeBuffers.TryEnqueue(segment))
            {
                actualEnqueued++;
            }
        }

        if (actualEnqueued > 0)
        {
            _ = Interlocked.Add(ref _totalBuffers, actualEnqueued);
        }
    }

    /// <summary>
    /// Performs the actual resource cleanup.
    /// Slab-backed segments do NOT need to be returned to ArrayPool.
    /// The underlying pinned arrays will be freed by the GC when all references are dropped.
    /// Fallback buffers (rented during AcquireBuffer miss path) ARE owned by the system ArrayPool
    /// but tracking which is which is infeasible. The GC handles slab memory; this just frees the ring.
    /// </summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Simply drain all segment references from the ring.
                // For slab-allocated segments: backing array will be GC'd.
                // For ArrayPool fallback segments: we accept a small one-time leak on shutdown.
                _ = _freeBuffers.DrainAll();

                _ = s_pools.TryRemove(_bufferSize, out _);
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Creates a snapshot of current pool state.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BufferPoolState CreatePoolStateSnapshot()
    {
        return new BufferPoolState
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = Volatile.Read(ref _misses),
            Hits = Volatile.Read(ref _hits)
        };
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryBeginOptimize() => Interlocked.CompareExchange(ref _isOptimizing, 1, 0) == 0;

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EndOptimize() => Volatile.Write(ref _isOptimizing, 0);

    #endregion Private Helpers

    #region Private Class

    private sealed class BufferRing
    {
        private ArraySegment<byte>[] _slots;
        private int _head;
        private int _tail;
        private int _count;
        private SpinLock _lock;

        public int Count => Volatile.Read(ref _count);

        public BufferRing(int capacity)
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

    #endregion Private Class
}
