using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Manages a pool of shared buffers with optimized memory handling.
/// </summary>
public sealed class BufferPoolShared : IDisposable
{
    private static readonly ConcurrentDictionary<int, BufferPoolShared> Pools = new();
    private readonly ConcurrentQueue<byte[]> _freeBuffers;
    private readonly ArrayPool<byte> _arrayPool;
    private readonly int _bufferSize;
    private readonly Lock _disposeLock = new();

    private BufferInfo _poolInfo;
    private int _totalBuffers;
    private bool _disposed;
    private int _misses;
    private bool _isOptimizing;

    /// <summary>
    /// The total number of buffers in the pool.
    /// </summary>
    public int TotalBuffers => Volatile.Read(ref _totalBuffers);

    /// <summary>
    /// The number of free buffers in the pool.
    /// </summary>
    public int FreeBuffers => _freeBuffers.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolShared"/> class.
    /// </summary>
    private BufferPoolShared(int bufferSize, int initialCapacity)
    {
        _bufferSize = bufferSize;
        _arrayPool = ArrayPool<byte>.Shared;
        _freeBuffers = new ConcurrentQueue<byte[]>();

        // Pre-allocate buffers for better initial performance
        PreallocateBuffers(initialCapacity);
    }

    /// <summary>
    /// Pre-allocates buffers to the specified capacity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreallocateBuffers(int capacity)
    {
        var buffers = new byte[capacity][];

        // Rent all buffers at once for better locality
        for (int i = 0; i < capacity; i++)
        {
            buffers[i] = _arrayPool.Rent(_bufferSize);
        }

        // Enqueue all buffers to the free queue
        foreach (var buffer in buffers)
        {
            _freeBuffers.Enqueue(buffer);
        }

        _totalBuffers = capacity;
    }

    /// <summary>
    /// Gets or creates a shared buffer pool for the specified buffer size.
    /// </summary>
    public static BufferPoolShared GetOrCreatePool(int bufferSize, int initialCapacity)
    {
        return Pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity));
    }

    /// <summary>
    /// Acquires a buffer from the pool with fast path optimization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] AcquireBuffer()
    {
        // Fast path - most common case
        if (_freeBuffers.TryDequeue(out var buffer))
        {
            return buffer;
        }

        // Slow path - need to rent new buffer
        Interlocked.Increment(ref _misses);
        Interlocked.Increment(ref _totalBuffers);

        return _arrayPool.Rent(_bufferSize);
    }

    /// <summary>
    /// Releases a buffer back into the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseBuffer(byte[] buffer)
    {
        // Validate buffer
        if (buffer == null || buffer.Length != _bufferSize)
        {
            throw new ArgumentException("Invalid buffer.");
        }

        // Clear sensitive data for security if needed
        // Array.Clear(buffer, 0, buffer.Length);

        _freeBuffers.Enqueue(buffer);
    }

    /// <summary>
    /// Increases the capacity of the pool by adding buffers.
    /// </summary>
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            throw new ArgumentException("The additional quantity must be greater than zero.");
        }

        if (_isOptimizing) return;

        try
        {
            _isOptimizing = true;

            // Batch allocation for better performance
            var buffersToAdd = new List<byte[]>(additionalCapacity);
            for (int i = 0; i < additionalCapacity; ++i)
            {
                buffersToAdd.Add(_arrayPool.Rent(_bufferSize));
            }

            foreach (var buffer in buffersToAdd)
            {
                _freeBuffers.Enqueue(buffer);
            }

            Interlocked.Add(ref _totalBuffers, additionalCapacity);
        }
        finally
        {
            _isOptimizing = false;
        }
    }

    /// <summary>
    /// Decreases the capacity of the pool by removing buffers.
    /// </summary>
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0) return;
        if (_isOptimizing) return;

        try
        {
            _isOptimizing = true;

            int removed = 0;
            int target = Math.Min(capacityToRemove, _freeBuffers.Count);

            // Use batch processing for better performance
            var buffersToReturn = new List<byte[]>(target);

            for (int i = 0; i < target; i++)
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
            foreach (var buffer in buffersToReturn)
            {
                _arrayPool.Return(buffer);
            }

            if (removed > 0)
            {
                Interlocked.Add(ref _totalBuffers, -removed);
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
    public BufferInfo GetPoolInfo()
    {
        return new BufferInfo
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = Volatile.Read(ref _misses)
        };
    }

    /// <summary>
    /// Gets information about the buffer pool by reference for better performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly BufferInfo GetPoolInfoRef()
    {
        // Cache the pool info in a private field (FIFO cache)
        _poolInfo = new BufferInfo
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = Volatile.Read(ref _totalBuffers),
            BufferSize = _bufferSize,
            Misses = Volatile.Read(ref _misses)
        };

        return ref _poolInfo;
    }

    /// <summary>
    /// Releases the buffer pool and returns all buffers to the array pool.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the actual resource cleanup.
    /// </summary>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        lock (_disposeLock)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Process buffers in batches for better performance
                const int batchSize = 64;
                var buffers = new byte[Math.Min(batchSize, _freeBuffers.Count)][];

                while (true)
                {
                    int count = 0;
                    while (count < buffers.Length && _freeBuffers.TryDequeue(out byte[]? buffer))
                    {
                        buffers[count] = buffer!;
                        count++;
                    }

                    if (count == 0) break;

                    for (int i = 0; i < count; i++)
                    {
                        _arrayPool.Return(buffers[i]);
                    }
                }

                Pools.TryRemove(_bufferSize, out _);
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer for cleanup when Dispose is not called.
    /// </summary>
    ~BufferPoolShared()
    {
        Dispose(false);
    }
}
