using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Manages shared buffer pools.
/// </summary>
public sealed class BufferManager : IDisposable
{
    private readonly ConcurrentDictionary<int, BufferPoolShared> _pools = new();
    private readonly ConcurrentDictionary<int, (int RentCounter, int ReturnCounter)> _adjustmentCounters = new();
    private int[] _sortedKeys = [];

    /// <summary>
    /// Event triggered when buffer pool needs to increase capacity.
    /// </summary>
    public event Action<BufferPoolShared>? EventIncrease;

    /// <summary>
    /// Event triggered when buffer pool needs to decrease capacity.
    /// </summary>
    public event Action<BufferPoolShared>? EventShrink;

    /// <summary>
    /// Creates a new buffer pool with a specified buffer size and initial capacity.
    /// </summary>
    /// <param name="bufferSize">The size of each buffer in the pool.</param>
    /// <param name="initialCapacity">The initial number of buffers to allocate.</param>
    public void CreatePool(int bufferSize, int initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity)))
        {
            // Update the sorted list of pool sizes
            _sortedKeys = [.. _pools.Keys.OrderBy(k => k)];
        }
    }

    /// <summary>
    /// Rents a buffer that is at least the requested size.
    /// </summary>
    /// <param name="size">The size of the buffer to rent.</param>
    /// <returns>A byte array representing the buffer.</returns>
    /// <exception cref="ArgumentException">Thrown if the requested buffer size exceeds the available pool size.</exception>
    public byte[] RentBuffer(int size)
    {
        int poolSize = FindSuitablePoolSize(size);
        if (poolSize == 0)
            throw new ArgumentException("Requested buffer size exceeds maximum available pool size.");

        BufferPoolShared pool = _pools[poolSize];
        byte[] buffer = pool.AcquireBuffer();

        // Check and trigger the event to increase capacity
        if (AdjustCounter(poolSize, isRent: true))
            EventIncrease?.Invoke(pool);

        return buffer;
    }

    /// <summary>
    /// Returns a buffer to the appropriate pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <exception cref="ArgumentException">Thrown if the buffer size is invalid.</exception>
    public void ReturnBuffer(byte[] buffer)
    {
        if (buffer == null || !_pools.TryGetValue(buffer.Length, out BufferPoolShared? pool))
            throw new ArgumentException("Invalid buffer size.");

        pool.ReleaseBuffer(buffer);

        // Check and trigger the event to shrink capacity
        if (AdjustCounter(buffer.Length, isRent: false))
            EventShrink?.Invoke(pool);
    }

    /// <summary>
    /// Adjusts the rent and return counters, and checks whether to trigger the increase or shrink capacity events.
    /// </summary>
    /// <param name="poolSize">The size of the pool.</param>
    /// <param name="isRent">Indicates whether it is a rent or return action.</param>
    /// <returns>True if an event should be triggered, otherwise false.</returns>
    private bool AdjustCounter(int poolSize, bool isRent)
    {
        return _adjustmentCounters.AddOrUpdate(poolSize,
            key => isRent ? (1, 0) : (0, 1),
            (key, current) =>
            {
                var newRentCounter = isRent ? current.RentCounter + 1 : current.RentCounter;
                var newReturnCounter = isRent ? current.ReturnCounter : current.ReturnCounter + 1;

                if (newRentCounter >= 10 && isRent)
                    return (0, current.ReturnCounter);

                if (newReturnCounter >= 10 && !isRent)
                    return (current.RentCounter, 0);

                return (newRentCounter, newReturnCounter);
            }).RentCounter == 0 || _adjustmentCounters[poolSize].ReturnCounter == 0;
    }

    /// <summary>
    /// Finds the most suitable pool size based on the requested size.
    /// </summary>
    /// <param name="size">The requested size.</param>
    /// <returns>The size of the most suitable pool.</returns>
    private int FindSuitablePoolSize(int size)
    {
        foreach (var key in _sortedKeys)
        {
            if (key >= size)
                return key;
        }

        return 0;
    }

    /// <summary>
    /// Releases all resources used by the buffer pools.
    /// </summary>
    public void Dispose()
    {
        foreach (var pool in _pools.Values)
        {
            pool.Dispose();
        }

        _pools.Clear();
        _sortedKeys = [];
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="BufferManager"/> class.
    /// </summary>
    ~BufferManager() => Dispose();
}
