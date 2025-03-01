using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Manages shared buffer pools with improved performance.
/// </summary>
public sealed class BufferManager : IDisposable
{
    private readonly ConcurrentDictionary<int, BufferPoolShared> _pools = new();
    private readonly ConcurrentDictionary<int, BufferCounters> _adjustmentCounters = new();
    private readonly ReaderWriterLockSlim _keysLock = new(LockRecursionPolicy.NoRecursion);

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
    /// Private structure to track buffer usage counters
    /// </summary>
    private struct BufferCounters
    {
        public int RentCounter;
        public int ReturnCounter;
    }

    /// <summary>
    /// Creates a new buffer pool with a specified buffer size and initial capacity.
    /// </summary>
    public void CreatePool(int bufferSize, int initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity)))
        {
            UpdateSortedKeys();
        }
    }

    /// <summary>
    /// Updates the sorted keys with proper locking
    /// </summary>
    private void UpdateSortedKeys()
    {
        _keysLock.EnterWriteLock();
        try
        {
            _sortedKeys = [.. _pools.Keys.OrderBy(k => k)];
        }
        finally
        {
            _keysLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Rents a buffer that is at least the requested size with optimized lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] RentBuffer(int size)
    {
        int poolSize = FindSuitablePoolSize(size);
        if (poolSize == 0)
            throw new ArgumentException($"Requested buffer size ({size}) exceeds maximum available pool size.");

        if (!_pools.TryGetValue(poolSize, out var pool))
            throw new InvalidOperationException($"Pool for size {poolSize} is not available.");

        byte[] buffer = pool.AcquireBuffer();

        // Check and trigger the event to increase capacity if needed
        if (AdjustCounter(poolSize, isRent: true))
            EventIncrease?.Invoke(pool);

        return buffer;
    }

    /// <summary>
    /// Returns a buffer to the appropriate pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnBuffer(byte[]? buffer)
    {
        if (buffer == null) return;

        if (!_pools.TryGetValue(buffer.Length, out BufferPoolShared? pool))
            throw new ArgumentException($"Invalid buffer size: {buffer.Length}.");

        pool.ReleaseBuffer(buffer);

        // Check and trigger the event to shrink capacity if needed
        if (AdjustCounter(buffer.Length, isRent: false))
            EventShrink?.Invoke(pool);
    }

    /// <summary>
    /// Adjusts the rent and return counters with optimized logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool AdjustCounter(int poolSize, bool isRent)
    {
        BufferCounters newCounters = default;
        bool shouldTriggerEvent = false;

        _adjustmentCounters.AddOrUpdate(
            poolSize,
            // Add function - when key doesn't exist
            _ =>
            {
                shouldTriggerEvent = true;
                return isRent ? new BufferCounters { RentCounter = 1, ReturnCounter = 0 }
                             : new BufferCounters { RentCounter = 0, ReturnCounter = 1 };
            },
            // Update function - when key exists
            (_, current) =>
            {
                newCounters = current;

                // Update appropriate counter
                if (isRent)
                {
                    newCounters.RentCounter++;
                    // Check if we've reached the threshold to trigger
                    if (newCounters.RentCounter >= 10)
                    {
                        shouldTriggerEvent = true;
                        newCounters.RentCounter = 0; // Reset counter
                    }
                }
                else
                {
                    newCounters.ReturnCounter++;
                    // Check if we've reached the threshold to trigger
                    if (newCounters.ReturnCounter >= 10)
                    {
                        shouldTriggerEvent = true;
                        newCounters.ReturnCounter = 0; // Reset counter
                    }
                }

                return newCounters;
            });

        return shouldTriggerEvent;
    }

    /// <summary>
    /// Finds the most suitable pool size with optimized binary search.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindSuitablePoolSize(int size)
    {
        // Use a local reference to avoid potential threading issues
        _keysLock.EnterReadLock();
        int[] keys;
        try
        {
            keys = _sortedKeys;
        }
        finally
        {
            _keysLock.ExitReadLock();
        }

        // Quick check for empty array
        if (keys.Length == 0)
            return 0;

        // Quick check for size smaller than first key or larger than last key
        if (size <= keys[0])
            return keys[0];

        if (size > keys[^1])
            return 0;

        // Binary search for efficiency with large number of pools
        int left = 0;
        int right = keys.Length - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (keys[mid] == size)
                return keys[mid];

            if (keys[mid] < size)
                left = mid + 1;
            else
                right = mid - 1;
        }

        // At this point, left > right and left is the insertion point
        // Return the next larger key (ceiling value)
        return left < keys.Length ? keys[left] : 0;
    }

    /// <summary>
    /// Releases all resources used by the buffer pools.
    /// </summary>
    public void Dispose()
    {
        // Dispose all pools in parallel for faster cleanup on large systems
        Parallel.ForEach(_pools.Values, pool =>
        {
            pool.Dispose();
        });

        _pools.Clear();
        _adjustmentCounters.Clear();

        _keysLock.EnterWriteLock();
        try
        {
            _sortedKeys = [];
        }
        finally
        {
            _keysLock.ExitWriteLock();
            _keysLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
