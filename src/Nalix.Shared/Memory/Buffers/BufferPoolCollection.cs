using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages shared buffer pools.
/// </summary>
public sealed class BufferPoolCollection : IDisposable
{
    #region Fields

    private readonly ConcurrentDictionary<Int32, BufferPoolShared> _pools = new();
    private readonly ConcurrentDictionary<Int32, BufferCounters> _adjustmentCounters = new();
    private readonly ReaderWriterLockSlim _keysLock = new(LockRecursionPolicy.NoRecursion);

    private Int32[] _sortedKeys = [];

    #endregion Fields

    #region Properties

    /// <summary>
    /// Event triggered when buffer pool needs to increase capacity.
    /// </summary>
    public event Action<BufferPoolShared>? EventIncrease;

    /// <summary>
    /// Event triggered when buffer pool needs to decrease capacity.
    /// </summary>
    public event Action<BufferPoolShared>? EventShrink;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Private structure to track buffer usage counters
    /// </summary>
    private unsafe struct BufferCounters
    {
        private fixed Int32 _counters[2]; // [rent, return]

        public Int32 RentCounter
        {
            get
            {
                fixed (Int32* ptr = _counters)
                {
                    return ptr[0];
                }
            }
            set
            {
                fixed (Int32* ptr = _counters)
                {
                    ptr[0] = value;
                }
            }
        }

        public Int32 ReturnCounter
        {
            get
            {
                fixed (Int32* ptr = _counters)
                {
                    return ptr[1];
                }
            }
            set
            {
                fixed (Int32* ptr = _counters)
                {
                    ptr[1] = value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int32 IncrementRent()
        {
            fixed (Int32* ptr = _counters)
            {
                return Interlocked.Increment(ref ptr[0]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int32 IncrementReturn()
        {
            fixed (Int32* ptr = _counters)
            {
                return Interlocked.Increment(ref ptr[1]);
            }
        }
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Creates a new buffer pool with a specified buffer size and initial capacity.
    /// </summary>
    public void CreatePool(Int32 bufferSize, Int32 initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity)))
        {
            this.UpdateSortedKeys();
        }
    }

    /// <summary>
    /// Updates the sorted keys with proper locking
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    public Byte[] RentBuffer(Int32 size)
    {
        Int32 poolSize = FindSuitablePoolSize(size);
        if (poolSize == 0)
        {
            throw new ArgumentException($"Requested buffer size ({size}) exceeds maximum available pool size.");
        }

        if (!_pools.TryGetValue(poolSize, out var pool))
        {
            throw new InvalidOperationException($"Pools for size {poolSize} is not available.");
        }

        Byte[] buffer = pool.AcquireBuffer();

        // Check and trigger the event to increase capacity if needed
        if (AdjustCounter(poolSize, isRent: true))
        {
            EventIncrease?.Invoke(pool);
        }

        return buffer;
    }

    /// <summary>
    /// Returns a buffer to the appropriate pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnBuffer(Byte[]? buffer)
    {
        if (buffer == null)
        {
            return;
        }

        if (!_pools.TryGetValue(buffer.Length, out BufferPoolShared? pool))
        {
            throw new ArgumentException($"Invalid buffer size: {buffer.Length}.");
        }

        pool.ReleaseBuffer(buffer);

        // Check and trigger the event to shrink capacity if needed
        if (AdjustCounter(buffer.Length, isRent: false))
        {
            EventShrink?.Invoke(pool);
        }
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Adjusts the rent and return counters with optimized logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Boolean AdjustCounter(Int32 poolSize, Boolean isRent)
    {
        BufferCounters newCounters = default;
        Boolean shouldTriggerEvent = false;

        _ = _adjustmentCounters.AddOrUpdate(
            poolSize,
            // Push function - when key doesn't exist
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
    private unsafe Int32 FindSuitablePoolSize(Int32 size)
    {
        _keysLock.EnterReadLock();
        try
        {
            var keys = _sortedKeys.AsSpan();

            if (keys.Length == 0)
            {
                return 0;
            }

            if (size <= keys[0])
            {
                return keys[0];
            }

            if (size > keys[^1])
            {
                return 0;
            }

            Int32 index = keys.BinarySearch(size);

            return index >= 0
                ? keys[index]
                : (~index < keys.Length ? keys[~index] : 0);
        }
        finally
        {
            _keysLock.ExitReadLock();
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the buffer pools.
    /// </summary>
    public void Dispose()
    {
        // Dispose all pools in parallel for faster cleanup on large systems
        _ = Parallel.ForEach(_pools.Values, pool =>
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

    #endregion IDisposable
}