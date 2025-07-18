using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages a pool of shared buffers with optimized memory handling.
/// </summary>
public sealed class BufferPoolShared : IDisposable
{
    #region Fields

    private static readonly ConcurrentDictionary<Int32, BufferPoolShared> Pools = new();
    private readonly ConcurrentQueue<Byte[]> _freeBuffers;
    private readonly ArrayPool<Byte> _arrayPool;
    private readonly Int32 _bufferSize;
    private readonly Lock _disposeLock = new();

    private BufferPoolSnapshot _poolInfo;
    private Int32 _totalBuffers;
    private Boolean _disposed;
    private Int32 _misses;
    private Boolean _isOptimizing;

    #endregion Fields

    #region Properties

    /// <summary>
    /// The total Number of buffers in the pool.
    /// </summary>
    public Int32 TotalBuffers => Volatile.Read(ref _totalBuffers);

    /// <summary>
    /// The Number of free buffers in the pool.
    /// </summary>
    public Int32 FreeBuffers => _freeBuffers.Count;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolShared"/> class.
    /// </summary>
    private BufferPoolShared(Int32 bufferSize, Int32 initialCapacity)
    {
        _bufferSize = bufferSize;
        _arrayPool = ArrayPool<Byte>.Shared;
        _freeBuffers = new ConcurrentQueue<Byte[]>();

        // Pre-allocate buffers for better initial performance
        this.PreallocateBuffers(initialCapacity);
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets or creates a shared buffer pool for the specified buffer size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferPoolShared GetOrCreatePool(Int32 bufferSize, Int32 initialCapacity)
        => Pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity));

    /// <summary>
    /// Acquires a buffer from the pool with fast path optimization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] AcquireBuffer()
    {
        // Fast path - most common case
        if (_freeBuffers.TryDequeue(out var buffer))
        {
            return buffer;
        }

        // Slow path - need to rent new buffer
        _ = Interlocked.Increment(ref _misses);
        _ = Interlocked.Increment(ref _totalBuffers);

        return _arrayPool.Rent(_bufferSize);
    }

    /// <summary>
    /// Releases a buffer back into the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void ReleaseBuffer(Byte[] buffer)
    {
        // Validate buffer
        if (buffer is null || Unsafe.SizeOf<Byte>() * buffer.Length != _bufferSize)
        {
            throw new ArgumentException("Invalid buffer.");
        }

        // Dispose sensitive data for security if needed
        // ClearBuffer(buffer);

        _freeBuffers.Enqueue(buffer);
    }

    /// <summary>
    /// Increases the capacity of the pool by adding buffers.
    /// </summary>
    public void IncreaseCapacity(Int32 additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            throw new ArgumentException("The additional quantity must be greater than zero.");
        }

        if (_isOptimizing)
        {
            return;
        }

        try
        {
            _isOptimizing = true;

            // Batch allocation for better performance
            List<Byte[]> buffersToAdd = new(additionalCapacity);
            for (Int32 i = 0; i < additionalCapacity; ++i)
            {
                buffersToAdd.Add(_arrayPool.Rent(_bufferSize));
            }

            foreach (Byte[] buffer in buffersToAdd)
            {
                _freeBuffers.Enqueue(buffer);
            }

            _ = Interlocked.Add(ref _totalBuffers, additionalCapacity);
        }
        finally
        {
            _isOptimizing = false;
        }
    }

    /// <summary>
    /// Decreases the capacity of the pool by removing buffers.
    /// </summary>
    public void DecreaseCapacity(Int32 capacityToRemove)
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

            Int32 removed = 0;
            Int32 target = Math.Min(capacityToRemove, _freeBuffers.Count);

            // Use batch processing for better performance
            var buffersToReturn = new List<Byte[]>(target);

            for (Int32 i = 0; i < target; i++)
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
            foreach (Byte[] buffer in buffersToReturn)
            {
                _arrayPool.Return(buffer);
            }

            if (removed > 0)
            {
                _ = Interlocked.Add(ref _totalBuffers, -removed);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferPoolSnapshot GetPoolInfo()
        => new()
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = Volatile.Read(ref _misses)
        };

    /// <summary>
    /// Gets information about the buffer pool by reference for better performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly BufferPoolSnapshot GetPoolInfoRef()
    {
        // Caches the pool info in a private field (FIFO cache)
        _poolInfo = new BufferPoolSnapshot
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = Volatile.Read(ref _misses)
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

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ClearBuffer(Byte[] buffer)
    {
        fixed (Byte* ptr = buffer)
        {
            Unsafe.InitBlock(ptr, 0, (UInt32)buffer.Length);
        }
    }

    /// <summary>
    /// Pre-allocates buffers to the specified capacity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreallocateBuffers(Int32 capacity)
    {
        Byte[][] buffers = new Byte[capacity][];

        // Rent all buffers at once for better locality
        for (Int32 i = 0; i < capacity; i++)
        {
            buffers[i] = _arrayPool.Rent(_bufferSize);
        }

        // Enqueue all buffers to the free queue
        foreach (Byte[] buffer in buffers)
        {
            _freeBuffers.Enqueue(buffer);
        }

        _totalBuffers = capacity;
    }

    /// <summary>
    /// Performs the actual resource cleanup.
    /// </summary>
    private void Dispose(Boolean disposing)
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
                const Int32 batchSize = 64;
                var buffers = new Byte[Math.Min(batchSize, _freeBuffers.Count)][];

                while (true)
                {
                    Int32 count = 0;
                    while (count < buffers.Length && _freeBuffers.TryDequeue(out Byte[]? buffer))
                    {
                        buffers[count] = buffer!;
                        count++;
                    }

                    if (count == 0)
                    {
                        break;
                    }

                    for (Int32 i = 0; i < count; i++)
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