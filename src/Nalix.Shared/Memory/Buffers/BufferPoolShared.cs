// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages a pool of shared buffers with optimized memory handling.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.DebuggerDisplay("Size={_bufferSize}, Total={_totalBuffers}, Free={_freeBuffers.Count}")]
internal sealed class BufferPoolShared : System.IDisposable
{
    #region Fields

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, BufferPoolShared> Pools = new();

    private readonly BufferRing _freeBuffers;
    private readonly System.Int32 _bufferSize;
    private readonly System.Boolean _secureClear;
    private readonly System.Threading.Lock _disposeLock;
    private readonly System.Buffers.ArrayPool<System.Byte> _arrayPool;

    private System.Int32 _misses;
    private System.Boolean _disposed;
    private BufferPoolState _poolInfo;
    private System.Int32 _totalBuffers;
    private System.Int32 _isOptimizing;

    #endregion Fields

    #region Properties

    /// <summary>
    /// The total number of buffers in the pool.
    /// </summary>
    public System.Int32 TotalBuffers => System.Threading.Volatile.Read(ref _totalBuffers);

    /// <summary>
    /// The number of free buffers in the pool.
    /// </summary>
    public System.Int32 FreeBuffers => _freeBuffers.Count;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolShared"/> class.
    /// </summary>
    private BufferPoolShared(System.Int32 bufferSize, System.Int32 initialCapacity, System.Boolean secureClear)
    {
        _disposeLock = new();
        _bufferSize = bufferSize;
        _secureClear = secureClear;
        _arrayPool = System.Buffers.ArrayPool<System.Byte>.Shared;

        System.Int32 ringCapacity = initialCapacity <= 0
            ? 4
            : (System.Int32)System.Numerics.BitOperations.RoundUpToPowerOf2((System.UInt32)initialCapacity);

        _freeBuffers = new BufferRing(ringCapacity);

        PreallocateBuffers(initialCapacity);
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Gets or creates a shared buffer pool for the specified buffer size.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static BufferPoolShared GetOrCreatePool(System.Int32 bufferSize, System.Int32 initialCapacity, System.Boolean secureClear)
        => Pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity, secureClear));

    /// <summary>
    /// Acquires a buffer from the pool with fast path optimization.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] AcquireBuffer()
    {
        if (_freeBuffers.TryDequeue(out System.Byte[]? buffer) && buffer is not null)
        {
            return buffer;
        }

        _ = System.Threading.Interlocked.Increment(ref _misses);
        _ = System.Threading.Interlocked.Increment(ref _totalBuffers);

        return _arrayPool.Rent(_bufferSize);
    }

    /// <summary>
    /// Releases a buffer back into the pool.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe void ReleaseBuffer(System.Byte[] buffer)
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void IncreaseCapacity(System.Int32 additionalCapacity)
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void DecreaseCapacity(System.Int32 capacityToRemove)
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
            System.Int32 removed = 0;
            System.Int32 target = System.Math.Min(capacityToRemove, _freeBuffers.Count);

            System.Collections.Generic.List<System.Byte[]> buffersToReturn = new(target);

            for (System.Int32 i = 0; i < target; i++)
            {
                if (_freeBuffers.TryDequeue(out System.Byte[]? buf))
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
    private unsafe void ClearBuffer(System.Byte[] buffer)
    {
        fixed (System.Byte* ptr = buffer)
        {
            System.Runtime.CompilerServices.Unsafe.InitBlock(ptr, 0, (System.UInt32)buffer.Length);
        }
    }

    /// <summary>
    /// Pre-allocates buffers to the specified capacity.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void PreallocateBuffers(System.Int32 capacity)
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void RentAndEnqueueBuffers(System.Int32 count)
    {
        if (count <= 0)
        {
            return;
        }

        System.Collections.Generic.List<System.Byte[]> rented = new(count);
        for (System.Int32 i = 0; i < count; ++i)
        {
            rented.Add(_arrayPool.Rent(_bufferSize));
        }

        foreach (System.Byte[] buf in rented)
        {
            _ = _freeBuffers.TryEnqueue(buf);
        }

        _ = System.Threading.Interlocked.Add(ref _totalBuffers, count);
    }

    /// <summary>
    /// Returns a collection of buffers to the ArrayPool, optionally clearing them.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ReturnBuffersToArrayPool(System.Collections.Generic.List<System.Byte[]> buffers)
    {
        for (System.Int32 i = 0; i < buffers.Count; i++)
        {
            System.Byte[] buf = buffers[i];
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ReturnBuffersToArrayPool(System.Byte[][] buffers)
    {
        for (System.Int32 i = 0; i < buffers.Length; i++)
        {
            System.Byte[] buf = buffers[i];
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void Dispose(System.Boolean disposing)
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
                System.Byte[][] buffers = _freeBuffers.DrainAll();
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
    private System.Boolean TryBeginOptimize() => System.Threading.Interlocked.CompareExchange(ref _isOptimizing, 1, 0) == 0;

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void EndOptimize() => System.Threading.Volatile.Write(ref _isOptimizing, 0);

    #endregion Private Helpers

    #region Private Class

    private sealed class BufferRing
    {
        private System.Byte[][] _slots;
        private System.Int32 _head;
        private System.Int32 _tail;
        private System.Int32 _count;
        private System.Threading.SpinLock _lock;

        public BufferRing(System.Int32 capacity)
        {
            if (capacity <= 0)
            {
                capacity = 4;
            }

            _slots = new System.Byte[capacity][];
            _head = 0;
            _tail = 0;
            _count = 0;
            _lock = new System.Threading.SpinLock(enableThreadOwnerTracking: false);
        }

        public System.Int32 Count
        {
            get
            {
                System.Boolean taken = false;
                try
                {
                    _lock.Enter(ref taken);
                    return _count;
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

        public System.Boolean TryEnqueue(System.Byte[] buffer)
        {
            System.Boolean taken = false;
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

        public System.Boolean TryDequeue(
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[]? buffer)
        {
            System.Boolean taken = false;
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

        public void EnsureCapacity(System.Int32 targetCapacity)
        {
            System.Boolean taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (targetCapacity <= _slots.Length)
                {
                    return;
                }

                System.UInt32 newSize = System.Numerics.BitOperations.RoundUpToPowerOf2((System.UInt32)targetCapacity);

                System.Byte[][] newSlots = new System.Byte[newSize][];

                for (System.Int32 i = 0; i < _count; ++i)
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
        public System.Byte[][] DrainAll()
        {
            System.Boolean taken = false;
            try
            {
                _lock.Enter(ref taken);

                if (_count == 0)
                {
                    return System.Array.Empty<System.Byte[]>();
                }

                System.Byte[][] result = new System.Byte[_count][];
                for (System.Int32 i = 0; i < _count; ++i)
                {
                    System.Int32 index = (_head + i) & (_slots.Length - 1);
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
