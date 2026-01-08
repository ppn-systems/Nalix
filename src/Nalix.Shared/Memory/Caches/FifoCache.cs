// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Caches;

/// <summary>
/// A high-performance First-In-First-Out (FIFO) cache optimized for real-time server environments.
/// </summary>
/// <typeparam name="T">The type of elements stored in the cache.</typeparam>
[System.Diagnostics.DebuggerDisplay("Count={Count}, Capacity={Capacity}, IsFull={IsFull}, IsEmpty={IsEmpty}")]
public sealed class FifoCache<T> : System.IDisposable, System.Collections.Generic.IEnumerable<T>
{
    #region Fields

    private readonly System.Collections.Concurrent.ConcurrentQueue<T> _queue;
    private System.Int32 _currentSize;
    private readonly System.Threading.ReaderWriterLockSlim _cacheLock;
    private System.Boolean _isDisposed;

    // Caches statistics

    private System.Int64 _additions;
    private System.Int64 _removals;
    private System.Int64 _trimOperations;
    private readonly System.Diagnostics.Stopwatch _uptime = System.Diagnostics.Stopwatch.StartNew();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the maximum capacity of the cache.
    /// </summary>
    public System.Int32 Capacity { get; }

    /// <summary>
    /// Gets the ProtocolType of elements currently stored in the cache.
    /// </summary>
    public System.Int32 Count => System.Threading.Volatile.Read(ref _currentSize);

    /// <summary>
    /// Gets the ProtocolType of items added to the cache.
    /// </summary>
    public System.Int64 Additions => System.Threading.Interlocked.Read(ref _additions);

    /// <summary>
    /// Gets the ProtocolType of items removed from the cache.
    /// </summary>
    public System.Int64 Removals => System.Threading.Interlocked.Read(ref _removals);

    /// <summary>
    /// Gets the ProtocolType of trim operations performed on the cache.
    /// </summary>
    public System.Int64 TrimOperations => System.Threading.Interlocked.Read(ref _trimOperations);

    /// <summary>
    /// Gets the uptime of the cache in milliseconds.
    /// </summary>
    public System.Int64 UptimeMs => _uptime.ElapsedMilliseconds;

    /// <summary>
    /// Gets a value indicating whether the cache is empty.
    /// </summary>
    public System.Boolean IsEmpty => Count == 0;

    /// <summary>
    /// Gets a value indicating whether the cache is at capacity.
    /// </summary>
    public System.Boolean IsFull => Count >= Capacity;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="FifoCache{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum ProtocolType of elements the cache can hold.</param>
    /// <exception cref="System.ArgumentException">Thrown when the capacity is less than or equal to zero.</exception>
    public FifoCache(System.Int32 capacity)
    {
        if (capacity <= 0)
        {
            throw new System.ArgumentException("Capacity must be greater than zero.", nameof(capacity));
        }

        this.Capacity = capacity;

        _currentSize = 0;
        _queue = new System.Collections.Concurrent.ConcurrentQueue<T>();
        _cacheLock = new(System.Threading.LockRecursionPolicy.NoRecursion);
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Adds an element to the cache. If the cache is at capacity, the oldest element is removed.
    /// </summary>
    /// <param name="item">The element to add to the cache.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Push(T item)
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        _queue.Enqueue(item);
        _ = System.Threading.Interlocked.Increment(ref _currentSize);
        _ = System.Threading.Interlocked.Increment(ref _additions);

        TrimExcess();
    }

    /// <summary>
    /// Adds multiple elements to the cache in a batch operation.
    /// </summary>
    /// <param name="items">The elements to add to the cache.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the items collection is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Push(System.Collections.Generic.IEnumerable<T> items)
    {
        System.ArgumentNullException.ThrowIfNull(items);
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        System.Int32 addedCount = 0;
        foreach (var item in items)
        {
            _queue.Enqueue(item);
            addedCount++;
        }

        if (addedCount > 0)
        {
            _ = System.Threading.Interlocked.Add(ref _currentSize, addedCount);
            _ = System.Threading.Interlocked.Add(ref _additions, addedCount);
            TrimExcess();
        }
    }

    /// <summary>
    /// Removes and returns the oldest element from the cache.
    /// </summary>
    /// <returns>The oldest element in the cache.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the cache is empty.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public T Pop()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        return !TryPop(out T? result) ? throw new System.InvalidOperationException("FifoCache is empty.") : result!;
    }

    /// <summary>
    /// Attempts to remove and return the oldest element from the cache.
    /// </summary>
    /// <param name="value">When this method returns, contains the oldest element in the cache, if the cache is not empty; otherwise, the default value for the type of the value parameter.</param>
    /// <returns><c>true</c> if the oldest element was removed and returned successfully; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryPop(out T? value)
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        if (_queue.TryDequeue(out value))
        {
            _ = System.Threading.Interlocked.Decrement(ref _currentSize);
            _ = System.Threading.Interlocked.Increment(ref _removals);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to peek at the oldest element in the cache without removing it.
    /// </summary>
    /// <param name="value">When this method returns, contains the oldest element in the cache, if the cache is not empty; otherwise, the default value for the type of the value parameter.</param>
    /// <returns><c>true</c> if the oldest element was retrieved successfully; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryPeek(out T? value)
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));
        return _queue.TryPeek(out value);
    }

    /// <summary>
    /// Attempts to retrieve multiple elements from the cache in a batch operation.
    /// </summary>
    /// <param name="count">The ProtocolType of elements to retrieve.</param>
    /// <returns>A list containing the retrieved elements.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the count is less than or equal to zero.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> PopBatch(System.Int32 count)
    {
        if (count <= 0)
        {
            throw new System.ArgumentException("Count must be greater than zero.", nameof(count));
        }

        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        System.Collections.Generic.List<T> result = new(System.Math.Min(count, Count));
        System.Int32 retrieved = 0;

        for (System.Int32 i = 0; i < count; i++)
        {
            if (TryPop(out T? item) && item is not null)
            {
                result.Add(item);
                retrieved++;
            }
            else
            {
                // No more items available
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Clears all elements from the cache.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        _cacheLock.EnterWriteLock();
        try
        {
            System.Int32 itemsCleared = Count;
            _queue.Clear();
            _ = System.Threading.Interlocked.Exchange(ref _currentSize, 0);
            _ = System.Threading.Interlocked.Add(ref _removals, itemsCleared);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Resets the cache statistics without clearing the cache elements.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        _ = System.Threading.Interlocked.Exchange(ref _additions, 0);
        _ = System.Threading.Interlocked.Exchange(ref _removals, 0);
        _ = System.Threading.Interlocked.Exchange(ref _trimOperations, 0);
        _uptime.Restart();
    }

    /// <summary>
    /// Gets a snapshot of the current cache statistics.
    /// </summary>
    /// <returns>A dictionary containing cache statistics.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetStatistics()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));

        return new System.Collections.Generic.Dictionary<System.String, System.Object>
        {
            ["Capacity"] = Capacity,
            ["Count"] = Count,
            ["Additions"] = Additions,
            ["Removals"] = Removals,
            ["TrimOperations"] = TrimOperations,
            ["UptimeMs"] = UptimeMs,
            ["IsEmpty"] = IsEmpty,
            ["IsFull"] = IsFull
        };
    }

    /// <summary>
    /// Converts the elements of the cache to an array.
    /// </summary>
    /// <remarks>This operation does not remove elements from the cache.</remarks>
    /// <returns>An array containing the elements of the cache.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T[] ToArray()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));
        return [.. _queue];
    }

    /// <summary>
    /// Returns an enumerator that iterates through the cache.
    /// </summary>
    /// <remarks>This operation does not remove elements from the cache.</remarks>
    /// <returns>An enumerator for the cache.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.IEnumerator<T> GetEnumerator()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<>));
        return _queue.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the cache.
    /// </summary>
    /// <returns>An enumerator for the cache.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Trims excess items if the cache exceeds its capacity.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void TrimExcess()
    {
        // Fast path if we know we're within capacity
        if (Count <= Capacity)
        {
            return;
        }

        // We need to trim elements
        _cacheLock.EnterWriteLock();
        try
        {
            System.Int32 excessCount = Count - Capacity;
            if (excessCount <= 0)
            {
                return;
            }

            System.Int32 removed = 0;
            for (System.Int32 i = 0; i < excessCount; i++)
            {
                if (_queue.TryDequeue(out _))
                {
                    removed++;
                }
                else
                {
                    // Queue is unexpectedly empty
                    break;
                }
            }

            // Update stats
            if (removed > 0)
            {
                _ = System.Threading.Interlocked.Add(ref _currentSize, -removed);
                _ = System.Threading.Interlocked.Add(ref _removals, removed);
                _ = System.Threading.Interlocked.Increment(ref _trimOperations);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Disposes of the resources used by the cache.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _cacheLock.Dispose();
        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
