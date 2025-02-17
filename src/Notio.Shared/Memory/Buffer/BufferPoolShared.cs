using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Manages a pool of shared buffers.
/// </summary>
public sealed class BufferPoolShared : IDisposable
{
    private static readonly ConcurrentDictionary<int, BufferPoolShared> Pools = new();
    private readonly ConcurrentQueue<byte[]> _freeBuffers;
    private readonly ArrayPool<byte> _arrayPool;
    private readonly Lock _disposeLock = new();
    private readonly int _bufferSize;

    private BufferInfo _poolInfo;
    private int _totalBuffers;
    private bool _disposed;
    private int _misses;

    /// <summary>
    /// The total number of buffers in the pool.
    /// </summary>
    public int TotalBuffers => _totalBuffers;

    /// <summary>
    /// The number of free buffers in the pool.
    /// </summary>
    public int FreeBuffers => _freeBuffers.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolShared"/> class.
    /// </summary>
    /// <param name="bufferSize">The size of each buffer in the pool.</param>
    /// <param name="initialCapacity">The initial number of buffers to allocate.</param>
    private BufferPoolShared(int bufferSize, int initialCapacity)
    {
        _bufferSize = bufferSize;
        _arrayPool = ArrayPool<byte>.Shared;
        _freeBuffers = new ConcurrentQueue<byte[]>();

        for (int i = 0; i < initialCapacity; ++i)
        {
            _freeBuffers.Enqueue(_arrayPool.Rent(bufferSize));
        }

        _totalBuffers = initialCapacity;
    }

    /// <summary>
    /// Gets or creates a shared buffer pool for the specified buffer size.
    /// </summary>
    /// <param name="bufferSize">The size of each buffer in the pool.</param>
    /// <param name="initialCapacity">The initial number of buffers to allocate.</param>
    /// <returns>A <see cref="BufferPoolShared"/> object for the specified buffer size.</returns>
    public static BufferPoolShared GetOrCreatePool(int bufferSize, int initialCapacity)
    {
        return Pools.GetOrAdd(bufferSize, size => new BufferPoolShared(size, initialCapacity));
    }

    /// <summary>
    /// Acquires a buffer from the pool.
    /// </summary>
    /// <returns>A byte array representing the buffer.</returns>
    public byte[] AcquireBuffer()
    {
        if (_freeBuffers.TryDequeue(out var buffer))
        {
            return buffer;
        }

        Interlocked.Increment(ref _misses);
        Interlocked.Increment(ref _totalBuffers);

        return _arrayPool.Rent(_bufferSize);
    }

    /// <summary>
    /// Releases a buffer back into the pool.
    /// </summary>
    /// <param name="buffer">The buffer to release.</param>
    public void ReleaseBuffer(byte[] buffer)
    {
        if (buffer == null || buffer.Length != _bufferSize)
        {
            throw new ArgumentException("Invalid buffer.");
        }

        _freeBuffers.Enqueue(buffer);
    }

    /// <summary>
    /// Increases the capacity of the pool by adding buffers.
    /// </summary>
    /// <param name="additionalCapacity">The number of buffers to add.</param>
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0)
        {
            throw new ArgumentException("The additional quantity must be greater than zero.");
        }

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

    /// <summary>
    /// Decreases the capacity of the pool by removing buffers.
    /// </summary>
    /// <param name="capacityToRemove">The number of buffers to remove.</param>
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0)
        {
            return;
        }

        for (int i = 0; i < capacityToRemove; ++i)
        {
            if (_freeBuffers.TryDequeue(out var buffer))
            {
                _arrayPool.Return(buffer);
                Interlocked.Decrement(ref _totalBuffers);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Gets information about the buffer pool and stores it in a private field.
    /// </summary>
    /// <returns>A read-only reference to a <see cref="BufferInfo"/> object.</returns>
    public BufferInfo GetPoolInfo()
    {
        return new BufferInfo
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = _totalBuffers,
            BufferSize = _bufferSize,
            Misses = _misses
        };
    }

    /// <summary>
    /// Gets information about the buffer pool and stores it in a private field.
    /// </summary>
    /// <returns>A read-only reference to a <see cref="BufferInfo"/> object.</returns>
    public ref readonly BufferInfo GetPoolInfoRef()
    {
        // Cache the pool info in a private field (FIFO cache)
        _poolInfo = new BufferInfo
        {
            FreeBuffers = _freeBuffers.Count,
            TotalBuffers = _totalBuffers,
            BufferSize = _bufferSize,
            Misses = _misses
        };

        return ref _poolInfo;
    }

    /// <summary>
    /// Releases the buffer pool and returns all buffers to the array pool.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the actual resource cleanup.
    /// </summary>
    /// <param name="disposing">Indicates whether cleanup is being called from Dispose.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        lock (_disposeLock)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Release managed resources
                while (_freeBuffers.TryDequeue(out var buffer))
                {
                    _arrayPool.Return(buffer);
                }

                Pools.TryRemove(_bufferSize, out _);
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer (only used if unmanaged resources need to be released).
    /// </summary>
    ~BufferPoolShared()
    {
        Dispose(disposing: false);
    }
}
