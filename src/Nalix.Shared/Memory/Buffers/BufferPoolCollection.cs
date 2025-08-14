// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages shared buffer pools.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.DebuggerDisplay("Pools={_pools.Count}, Keys={_sortedKeys.Length}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class BufferPoolCollection : System.IDisposable
{
    #region Fields

    private readonly System.Threading.ReaderWriterLockSlim _keysLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, BufferPoolShared> _pools;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, BufferCounters> _adjustmentCounters;

    private System.Int32[] _sortedKeys;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Event triggered when buffer pool needs to increase capacity.
    /// </summary>
    public event System.Action<BufferPoolShared>? EventIncrease;

    /// <summary>
    /// Event triggered when buffer pool needs to decrease capacity.
    /// </summary>
    public event System.Action<BufferPoolShared>? EventShrink;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolCollection"/> class.
    /// </summary>
    public BufferPoolCollection()
    {
        _sortedKeys = [];

        _pools = new();
        _adjustmentCounters = new();
        _keysLock = new(System.Threading.LockRecursionPolicy.NoRecursion);
    }

    /// <summary>
    /// Private structure to track buffer usage counters
    /// </summary>
    private unsafe struct BufferCounters
    {
        private fixed System.Int32 _counters[2]; // [rent, return]

        public System.Int32 RentCounter
        {
            get
            {
                fixed (System.Int32* ptr = _counters)
                {
                    return ptr[0];
                }
            }
            set
            {
                fixed (System.Int32* ptr = _counters)
                {
                    ptr[0] = value;
                }
            }
        }

        public System.Int32 ReturnCounter
        {
            get
            {
                fixed (System.Int32* ptr = _counters)
                {
                    return ptr[1];
                }
            }
            set
            {
                fixed (System.Int32* ptr = _counters)
                {
                    ptr[1] = value;
                }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Int32 IncrementRent()
        {
            fixed (System.Int32* ptr = _counters)
            {
                return System.Threading.Interlocked.Increment(ref ptr[0]);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Int32 IncrementReturn()
        {
            fixed (System.Int32* ptr = _counters)
            {
                return System.Threading.Interlocked.Increment(ref ptr[1]);
            }
        }
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Creates a new buffer pool with a specified buffer size and initial capacity.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void CreatePool(System.Int32 bufferSize, System.Int32 initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity)))
        {
            this.UpdateSortedKeys();
        }
    }

    /// <summary>
    /// Updates the sorted keys with proper locking
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void UpdateSortedKeys()
    {
        _keysLock.EnterWriteLock();
        try
        {
            _sortedKeys = [.. System.Linq.Enumerable.OrderBy(_pools.Keys, k => k)];
        }
        finally
        {
            _keysLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Rents a buffer that is at least the requested size with optimized lookup.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] RentBuffer(System.Int32 size)
    {
        System.Int32 poolSize = FindSuitablePoolSize(size);
        if (poolSize == 0)
        {
            throw new System.ArgumentException($"Requested buffer size ({size}) exceeds maximum available pool size.");
        }

        if (!_pools.TryGetValue(poolSize, out var pool))
        {
            throw new System.InvalidOperationException($"Pools for size {poolSize} is not available.");
        }

        System.Byte[] buffer = pool.AcquireBuffer();

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ReturnBuffer(System.Byte[]? buffer)
    {
        if (buffer == null)
        {
            return;
        }

        if (!_pools.TryGetValue(buffer.Length, out BufferPoolShared? pool))
        {
            throw new System.ArgumentException($"Invalid buffer size: {buffer.Length}.");
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean AdjustCounter(System.Int32 poolSize, System.Boolean isRent)
    {
        BufferCounters newCounters = default;
        System.Boolean shouldTriggerEvent = false;

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private unsafe System.Int32 FindSuitablePoolSize(System.Int32 size)
    {
        _keysLock.EnterReadLock();
        try
        {
            System.Span<System.Int32> keys = System.MemoryExtensions.AsSpan(_sortedKeys);

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

            System.Int32 index = System.MemoryExtensions.BinarySearch(keys, size);

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
        _ = System.Threading.Tasks.Parallel.ForEach(_pools.Values, pool =>
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

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}