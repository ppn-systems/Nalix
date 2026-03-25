// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Memory.Internal.Buffers;

/// <summary>
/// Manages a pool of shared buffers with optimized memory handling.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.DebuggerDisplay("SIZE={_bufferSize}, Total={_totalBuffers}, Free={_freeBuffers.Count}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class BufferPoolShared : System.IDisposable
{
    #region Fields

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, BufferPoolShared> Pools = new();

    private readonly BufferRing _freeBuffers;
    private readonly int _bufferSize;
    private readonly bool _secureClear;
    private readonly System.Threading.Lock _disposeLock;
    private readonly System.Buffers.ArrayPool<byte> _arrayPool;

    private int _misses;
    private bool _disposed;
    private BufferPoolState _poolInfo;
    private int _totalBuffers;
    private int _isOptimizing;

    #endregion Fields

    #region Properties

    /// <summary>
    /// The total number of buffers in the pool.
    /// </summary>
    public int TotalBuffers => System.Threading.Volatile.Read(ref _totalBuffers);

    /// <summary>
    /// The number of free buffers in the pool.
    /// </summary>
    public int FreeBuffers => _freeBuffers.Count;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolShared"/> class.
    /// </summary>
    /// <param name="bufferSize"></param>
    /// <param name="initialCapacity"></param>
    /// <param name="secureClear"></param>
    private BufferPoolShared(int bufferSize, int initialCapacity, bool secureClear)
    {
        _disposeLock = new();
        _bufferSize = bufferSize;
        _secureClear = secureClear;
        _arrayPool = System.Buffers.ArrayPool<byte>.Shared;

        int ringCapacity = initialCapacity <= 0
            ? 4
            : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)initialCapacity);

        _freeBuffers = new BufferRing(ringCapacity);

        PreallocateBuffers(initialCapacity);
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Gets or creates a shared buffer pool for the specified buffer size.
    /// </summary>
    /// <param name="bufferSize"></param>
    /// <param name="initialCapacity"></param>
    /// <param name="secureClear"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static BufferPoolShared GetOrCreatePool(int bufferSize, int initialCapacity, bool secureClear)
        => Pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity, secureClear));

    /// <summary>
    /// Acquires a buffer from the pool with fast path optimization.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public byte[] AcquireBuffer()
    {
        if (_freeBuffers.TryDequeue(out byte[]? buffer) && buffer is not null)
        {
            return buffer;
        }

        _ = System.Threading.Interlocked.Increment(ref _misses);

        byte[] newBuffer = _arrayPool.Rent(_bufferSize);
        _ = System.Threading.Interlocked.Increment(ref _totalBuffers);

        return newBuffer;
    }

    /// <summary>
    /// Releases a buffer back into the pool.
    /// </summary>
    /// <param name="buffer"></param>
    /// <exception cref="System.ArgumentException"></exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ReleaseBuffer(byte[] buffer)
    {
        if (buffer is null || buffer.Length != _bufferSize)
        {
            throw new System.ArgumentException("Invalid buffer.");
        }

        if (_secureClear)
        {
            ClearBuffer(buffer);
        }

        if (!_freeBuffers.TryEnqueue(buffer))
        {
            _arrayPool.Return(buffer);
            _ = System.Threading.Interlocked.Decrement(ref _totalBuffers);
        }
    }

    /// <summary>
    /// Increases the capacity of the pool by adding buffers.
    /// </summary>
    /// <param name="additionalCapacity"></param>
    /// <exception cref="System.ArgumentException"></exception>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            throw new System.ArgumentException("The additional quantity must be greater than zero.");
        }

        if (!TryBeginOptimize())
        {
            return;
        }

        try
        {
            RentAndEnqueueBuffers(additionalCapacity);
        }
        finally
        {
            EndOptimize();
        }
    }

    /// <summary>
    /// Decreases the capacity of the pool by removing buffers.
    /// </summary>
    /// <param name="capacityToRemove"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0)
        {
            return;
        }

        if (!TryBeginOptimize())
        {
            return;
        }

        try
        {
            int removed = 0;
            int target = System.Math.Min(capacityToRemove, _freeBuffers.Count);

            System.Collections.Generic.List<byte[]> buffersToReturn = new(target);

            for (int i = 0; i < target; i++)
            {
                if (_freeBuffers.TryDequeue(out byte[]? buf))
                {
                    buffersToReturn.Add(buf);
                    removed++;
                }
                else
                {
                    break;
                }
            }

            if (buffersToReturn.Count > 0)
            {
                ReturnBuffersToArrayPool(buffersToReturn);
            }

            if (removed > 0)
            {
                _ = System.Threading.Interlocked.Add(ref _totalBuffers, -removed);
            }
        }
        finally
        {
            EndOptimize();
        }
    }

    /// <summary>
    /// Gets information about the buffer pool by value (cheap snapshot).
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public BufferPoolState GetPoolInfo() => CreatePoolStateSnapshot();

    /// <summary>
    /// Gets information about the buffer pool by reference for better performance.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ref readonly BufferPoolState GetPoolInfoRef()
    {
        _poolInfo = CreatePoolStateSnapshot();
        return ref _poolInfo;
    }

    #endregion Public API

    #region IDisposable

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for cleanup when Dispose is not called.
    /// </summary>
    ~BufferPoolShared()
    {
        Dispose(false);
    }

    #endregion IDisposable

    #region Private Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe void ClearBuffer(byte[] buffer)
    {
        fixed (byte* ptr = buffer)
        {
            System.Runtime.CompilerServices.Unsafe.InitBlock(ptr, 0, (uint)buffer.Length);
        }
    }

    /// <summary>
    /// Pre-allocates buffers to the specified capacity.
    /// </summary>
    /// <param name="capacity"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void PreallocateBuffers(int capacity)
    {
        if (capacity <= 0)
        {
            return;
        }

        _freeBuffers.EnsureCapacity(capacity);
        RentAndEnqueueBuffers(capacity);
    }

    /// <summary>
    /// Rents buffers from the ArrayPool and enqueues them into the ring.
    /// </summary>
    /// <param name="count"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void RentAndEnqueueBuffers(int count)
    {
        if (count <= 0)
        {
            return;
        }

        System.Collections.Generic.List<byte[]> rented = new(count);
        for (int i = 0; i < count; ++i)
        {
            rented.Add(_arrayPool.Rent(_bufferSize));
        }

        foreach (byte[] buf in rented)
        {
            _ = _freeBuffers.TryEnqueue(buf);
        }

        _ = System.Threading.Interlocked.Add(ref _totalBuffers, count);
    }

    /// <summary>
    /// Returns a collection of buffers to the ArrayPool, optionally clearing them.
    /// </summary>
    /// <param name="buffers"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ReturnBuffersToArrayPool(System.Collections.Generic.List<byte[]> buffers)
    {
        for (int i = 0; i < buffers.Count; i++)
        {
            byte[] buf = buffers[i];
            if (_secureClear)
            {
                ClearBuffer(buf);
            }

            _arrayPool.Return(buf);
        }
    }

    /// <summary>
    /// Returns a buffer array to the ArrayPool, optionally clearing them.
    /// </summary>
    /// <param name="buffers"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ReturnBuffersToArrayPool(byte[][] buffers)
    {
        for (int i = 0; i < buffers.Length; i++)
        {
            byte[] buf = buffers[i];
            if (_secureClear)
            {
                ClearBuffer(buf);
            }

            _arrayPool.Return(buf);
        }
    }

    /// <summary>
    /// Performs the actual resource cleanup.
    /// </summary>
    /// <param name="disposing"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
                // Drain all remaining buffers from ring and return them to ArrayPool.
                byte[][] buffers = _freeBuffers.DrainAll();
                if (buffers.Length > 0)
                {
                    ReturnBuffersToArrayPool(buffers);
                }

                _ = Pools.TryRemove(_bufferSize, out _);
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Creates a snapshot of current pool state.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private BufferPoolState CreatePoolStateSnapshot()
    {
        return new BufferPoolState
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = System.Threading.Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = System.Threading.Volatile.Read(ref _misses)
        };
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private bool TryBeginOptimize() => System.Threading.Interlocked.CompareExchange(ref _isOptimizing, 1, 0) == 0;

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void EndOptimize() => System.Threading.Volatile.Write(ref _isOptimizing, 0);

    #endregion Private Helpers

    #region Private Class

    private sealed class BufferRing
    {
        private byte[][] _slots;
        private int _head;
        private int _tail;
        private int _count;
        private System.Threading.SpinLock _lock;

        public int Count => System.Threading.Volatile.Read(ref _count);

        public BufferRing(int capacity)
        {
            if (capacity <= 0)
            {
                capacity = 4;
            }

            _head = 0;
            _tail = 0;
            _count = 0;
            _slots = new byte[capacity][];
            _lock = new System.Threading.SpinLock(enableThreadOwnerTracking: false);
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

        public bool TryDequeue(
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (_count == 0)
                {
                    buffer = null;
                    return false;
                }

                buffer = _slots[_head];
                _slots[_head] = null!;
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
        public byte[][] DrainAll()
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (_count == 0)
                {
                    return System.Array.Empty<byte[]>();
                }

                byte[][] result = new byte[_count][];
                for (int i = 0; i < _count; ++i)
                {
                    int index = (_head + i) & (_slots.Length - 1);
                    result[i] = _slots[index];
                    _slots[index] = null!;
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
