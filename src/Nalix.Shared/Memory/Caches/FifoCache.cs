using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Memory.Caches;

/// <summary>
/// A high-performance First-In-First-Out (FIFO) cache optimized for real-time server environments.
/// </summary>
/// <typeparam name="T">The type of elements stored in the cache.</typeparam>
public sealed class FifoCache<T> : IDisposable, IEnumerable<T>
{
    #region Fields

    private readonly ConcurrentQueue<T> _queue;
    private readonly int _capacity;
    private int _currentSize;
    private readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);
    private bool _isDisposed;

    // Caches statistics
    private long _additions;
    private long _removals;
    private long _trimOperations;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    #endregion

    #region Properties

    /// <summary>
    /// Gets the maximum capacity of the cache.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the Number of elements currently stored in the cache.
    /// </summary>
    public int Count => Volatile.Read(ref _currentSize);

    /// <summary>
    /// Gets the Number of items added to the cache.
    /// </summary>
    public long Additions => Interlocked.Read(ref _additions);

    /// <summary>
    /// Gets the Number of items removed from the cache.
    /// </summary>
    public long Removals => Interlocked.Read(ref _removals);

    /// <summary>
    /// Gets the Number of trim operations performed on the cache.
    /// </summary>
    public long TrimOperations => Interlocked.Read(ref _trimOperations);

    /// <summary>
    /// Gets the uptime of the cache in milliseconds.
    /// </summary>
    public long UptimeMs => _uptime.ElapsedMilliseconds;

    /// <summary>
    /// Gets a value indicating whether the cache is empty.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Gets a value indicating whether the cache is at capacity.
    /// </summary>
    public bool IsFull => Count >= Capacity;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="FifoCache{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum Number of elements the cache can hold.</param>
    /// <exception cref="ArgumentException">Thrown when the capacity is less than or equal to zero.</exception>
    public FifoCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));

        _capacity = capacity;
        _queue = new ConcurrentQueue<T>();
        _currentSize = 0;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds an element to the cache. If the cache is at capacity, the oldest element is removed.
    /// </summary>
    /// <param name="item">The element to add to the cache.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        _queue.Enqueue(item);
        Interlocked.Increment(ref _currentSize);
        Interlocked.Increment(ref _additions);

        TrimExcess();
    }

    /// <summary>
    /// Adds multiple elements to the cache in a batch operation.
    /// </summary>
    /// <param name="items">The elements to add to the cache.</param>
    /// <exception cref="ArgumentNullException">Thrown when the items collection is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        int addedCount = 0;
        foreach (var item in items)
        {
            _queue.Enqueue(item);
            addedCount++;
        }

        if (addedCount > 0)
        {
            Interlocked.Add(ref _currentSize, addedCount);
            Interlocked.Add(ref _additions, addedCount);
            TrimExcess();
        }
    }

    /// <summary>
    /// Trims excess items if the cache exceeds its capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrimExcess()
    {
        // Fast path if we know we're within capacity
        if (Count <= _capacity) return;

        // We need to trim elements
        _cacheLock.EnterWriteLock();
        try
        {
            int excessCount = Count - _capacity;
            if (excessCount <= 0) return;

            int removed = 0;
            for (int i = 0; i < excessCount; i++)
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
                Interlocked.Add(ref _currentSize, -removed);
                Interlocked.Add(ref _removals, removed);
                Interlocked.Increment(ref _trimOperations);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes and returns the oldest element from the cache.
    /// </summary>
    /// <returns>The oldest element in the cache.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the cache is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValue()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        if (!TryGetValue(out T? result))
            throw new InvalidOperationException("FifoCache is empty.");

        return result!;
    }

    /// <summary>
    /// Attempts to remove and return the oldest element from the cache.
    /// </summary>
    /// <param name="value">When this method returns, contains the oldest element in the cache, if the cache is not empty; otherwise, the default value for the type of the value parameter.</param>
    /// <returns><c>true</c> if the oldest element was removed and returned successfully; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T? value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        if (_queue.TryDequeue(out value))
        {
            Interlocked.Decrement(ref _currentSize);
            Interlocked.Increment(ref _removals);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to peek at the oldest element in the cache without removing it.
    /// </summary>
    /// <param name="value">When this method returns, contains the oldest element in the cache, if the cache is not empty; otherwise, the default value for the type of the value parameter.</param>
    /// <returns><c>true</c> if the oldest element was retrieved successfully; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeek(out T? value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));
        return _queue.TryPeek(out value);
    }

    /// <summary>
    /// Attempts to retrieve multiple elements from the cache in a batch operation.
    /// </summary>
    /// <param name="count">The Number of elements to retrieve.</param>
    /// <returns>A list containing the retrieved elements.</returns>
    /// <exception cref="ArgumentException">Thrown when the count is less than or equal to zero.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<T> GetBatch(int count)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be greater than zero.", nameof(count));

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        var result = new List<T>(Math.Min(count, Count));
        int retrieved = 0;

        for (int i = 0; i < count; i++)
        {
            if (TryGetValue(out T? item) && item is not null)
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
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        _cacheLock.EnterWriteLock();
        try
        {
            int itemsCleared = Count;
            _queue.Clear();
            Interlocked.Exchange(ref _currentSize, 0);
            Interlocked.Add(ref _removals, itemsCleared);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Resets the cache statistics without clearing the cache elements.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        Interlocked.Exchange(ref _additions, 0);
        Interlocked.Exchange(ref _removals, 0);
        Interlocked.Exchange(ref _trimOperations, 0);
        _uptime.Restart();
    }

    /// <summary>
    /// Gets a snapshot of the current cache statistics.
    /// </summary>
    /// <returns>A dictionary containing cache statistics.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object> GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));

        return new Dictionary<string, object>
        {
            ["Capacity"] = _capacity,
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
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] ToArray()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));
        return [.. _queue];
    }

    /// <summary>
    /// Returns an enumerator that iterates through the cache.
    /// </summary>
    /// <remarks>This operation does not remove elements from the cache.</remarks>
    /// <returns>An enumerator for the cache.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<T> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(FifoCache<T>));
        return _queue.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the cache.
    /// </summary>
    /// <returns>An enumerator for the cache.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes of the resources used by the cache.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _cacheLock.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
