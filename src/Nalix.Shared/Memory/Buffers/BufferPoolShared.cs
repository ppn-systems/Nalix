namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages a pool of shared buffers with optimized memory handling.
/// </summary>
public sealed class BufferPoolShared : System.IDisposable
{
    #region Fields

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, BufferPoolShared> Pools = new();

    private readonly System.Int32 _bufferSize;
    private readonly System.Threading.Lock _disposeLock;
    private readonly System.Buffers.ArrayPool<System.Byte> _arrayPool;
    private readonly System.Collections.Concurrent.ConcurrentQueue<System.Byte[]> _freeBuffers;

    private System.Int32 _misses;
    private System.Boolean _disposed;
    private BufferPoolState _poolInfo;
    private System.Int32 _totalBuffers;
    private System.Boolean _isOptimizing;

    #endregion Fields

    #region Properties

    /// <summary>
    /// The total TransportProtocol of buffers in the pool.
    /// </summary>
    public System.Int32 TotalBuffers => System.Threading.Volatile.Read(ref _totalBuffers);

    /// <summary>
    /// The TransportProtocol of free buffers in the pool.
    /// </summary>
    public System.Int32 FreeBuffers => _freeBuffers.Count;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolShared"/> class.
    /// </summary>
    private BufferPoolShared(System.Int32 bufferSize, System.Int32 initialCapacity)
    {
        _disposeLock = new();
        _bufferSize = bufferSize;
        _arrayPool = System.Buffers.ArrayPool<System.Byte>.Shared;
        _freeBuffers = new System.Collections.Concurrent.ConcurrentQueue<System.Byte[]>();

        // Pre-allocate buffers for better initial performance
        this.PreallocateBuffers(initialCapacity);
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets or creates a shared buffer pool for the specified buffer size.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static BufferPoolShared GetOrCreatePool(System.Int32 bufferSize, System.Int32 initialCapacity)
        => Pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity));

    /// <summary>
    /// Acquires a buffer from the pool with fast path optimization.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] AcquireBuffer()
    {
        // Fast path - most common case
        if (_freeBuffers.TryDequeue(out System.Byte[]? buffer))
        {
            return buffer;
        }

        // Slow path - need to rent new buffer
        _ = System.Threading.Interlocked.Increment(ref _misses);
        _ = System.Threading.Interlocked.Increment(ref _totalBuffers);

        return _arrayPool.Rent(_bufferSize);
    }

    /// <summary>
    /// Releases a buffer back into the pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe void ReleaseBuffer(System.Byte[] buffer)
    {
        // Validate buffer
        if (buffer is null ||
            System.Runtime.CompilerServices.Unsafe.SizeOf<System.Byte>() * buffer.Length != _bufferSize)
        {
            throw new System.ArgumentException("Invalid buffer.");
        }

        // Dispose sensitive data for security if needed
        // ClearBuffer(buffer);

        _freeBuffers.Enqueue(buffer);
    }

    /// <summary>
    /// Increases the capacity of the pool by adding buffers.
    /// </summary>
    public void IncreaseCapacity(System.Int32 additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            throw new System.ArgumentException("The additional quantity must be greater than zero.");
        }

        if (_isOptimizing)
        {
            return;
        }

        try
        {
            _isOptimizing = true;

            // Batch allocation for better performance
            System.Collections.Generic.List<System.Byte[]> buffersToAdd = new(additionalCapacity);
            for (System.Int32 i = 0; i < additionalCapacity; ++i)
            {
                buffersToAdd.Add(_arrayPool.Rent(_bufferSize));
            }

            foreach (System.Byte[] buffer in buffersToAdd)
            {
                _freeBuffers.Enqueue(buffer);
            }

            _ = System.Threading.Interlocked.Add(ref _totalBuffers, additionalCapacity);
        }
        finally
        {
            _isOptimizing = false;
        }
    }

    /// <summary>
    /// Decreases the capacity of the pool by removing buffers.
    /// </summary>
    public void DecreaseCapacity(System.Int32 capacityToRemove)
    {
        if (capacityToRemove <= 0)
        {
            return;
        }

        if (_isOptimizing)
        {
            return;
        }

        try
        {
            _isOptimizing = true;

            System.Int32 removed = 0;
            System.Int32 target = System.Math.Min(capacityToRemove, _freeBuffers.Count);

            // Use batch processing for better performance
            System.Collections.Generic.List<System.Byte[]> buffersToReturn = new(target);

            for (System.Int32 i = 0; i < target; i++)
            {
                if (_freeBuffers.TryDequeue(out var buffer))
                {
                    buffersToReturn.Add(buffer);
                    removed++;
                }
                else
                {
                    break;
                }
            }

            // Return buffers in batch
            foreach (System.Byte[] buffer in buffersToReturn)
            {
                _arrayPool.Return(buffer);
            }

            if (removed > 0)
            {
                _ = System.Threading.Interlocked.Add(ref _totalBuffers, -removed);
            }
        }
        finally
        {
            _isOptimizing = false;
        }
    }

    /// <summary>
    /// Gets information about the buffer pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public BufferPoolState GetPoolInfo()
        => new()
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = System.Threading.Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = System.Threading.Volatile.Read(ref _misses)
        };

    /// <summary>
    /// Gets information about the buffer pool by reference for better performance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ref readonly BufferPoolState GetPoolInfoRef()
    {
        // Caches the pool info in a private field (FIFO cache)
        _poolInfo = new BufferPoolState
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = System.Threading.Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = System.Threading.Volatile.Read(ref _misses)
        };

        return ref _poolInfo;
    }

    #endregion Public Methods

    #region IDisposable

    /// <summary>
    /// Releases the buffer pool and returns all buffers to the array pool.
    /// </summary>
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
        this.Dispose(false);
    }

    #endregion IDisposable

    #region Private Methods

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
    /// Pre-allocates buffers to the specified capacity
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void PreallocateBuffers(System.Int32 capacity)
    {
        System.Byte[][] buffers = new System.Byte[capacity][];

        // Rent all buffers at once for better locality
        for (System.Int32 i = 0; i < capacity; i++)
        {
            buffers[i] = _arrayPool.Rent(_bufferSize);
        }

        // Enqueue all buffers to the free queue
        foreach (System.Byte[] buffer in buffers)
        {
            _freeBuffers.Enqueue(buffer);
        }

        _totalBuffers = capacity;
    }

    /// <summary>
    /// Performs the actual resource cleanup.
    /// </summary>
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
                // Process buffers in batches for better performance
                const System.Int32 batchSize = 64;
                var buffers = new System.Byte[System.Math.Min(batchSize, _freeBuffers.Count)][];

                while (true)
                {
                    System.Int32 count = 0;
                    while (count < buffers.Length && _freeBuffers.TryDequeue(out System.Byte[]? buffer))
                    {
                        buffers[count] = buffer!;
                        count++;
                    }

                    if (count == 0)
                    {
                        break;
                    }

                    for (System.Int32 i = 0; i < count; i++)
                    {
                        _arrayPool.Return(buffers[i]);
                    }
                }

                _ = Pools.TryRemove(_bufferSize, out _);
            }

            _disposed = true;
        }
    }

    #endregion Private Methods
}