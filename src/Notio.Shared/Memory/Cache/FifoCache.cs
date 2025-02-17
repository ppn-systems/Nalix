using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Notio.Shared.Memory.Cache;

/// <summary>
/// Represents a First-In-First-Out (FIFO) cache.
/// </summary>
/// <typeparam name="T">The type of elements stored in the cache.</typeparam>
public sealed class FifoCache<T>
{
    private readonly ConcurrentQueue<T> _queue;
    private readonly int _capacity;
    private int _currentSize;

    /// <summary>
    /// Gets the number of elements currently stored in the cache.
    /// </summary>
    public int Count => _currentSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="FifoCache{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements the cache can hold.</param>
    /// <exception cref="ArgumentException">Thrown when the capacity is less than or equal to zero.</exception>
    public FifoCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));

        _capacity = capacity;
        _queue = new ConcurrentQueue<T>();
        _currentSize = 0;
    }

    /// <summary>
    /// Adds an element to the cache. If the cache is at capacity, the oldest element is removed.
    /// </summary>
    /// <param name="item">The element to add to the cache.</param>
    public void Add(T item)
    {
        _queue.Enqueue(item);

        // Increase current size
        Interlocked.Increment(ref _currentSize);

        // Remove oldest element if capacity is exceeded
        if (_currentSize <= _capacity) return;

        while (_currentSize > _capacity && _queue.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }

    /// <summary>
    /// Removes and returns the oldest element from the cache.
    /// </summary>
    /// <returns>The oldest element in the cache.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the cache is empty.</exception>
    public T GetValue()
    {
        if (!_queue.TryDequeue(out T? item)) throw new InvalidOperationException("FifoCache is empty.");
        Interlocked.Decrement(ref _currentSize);
        return item;

    }

    /// <summary>
    /// Attempts to remove and return the oldest element from the cache.
    /// </summary>
    /// <param name="value">When this method returns, contains the oldest element in the cache, if the cache is not empty; otherwise, the default value for the type of the value parameter.</param>
    /// <returns><c>true</c> if the oldest element was removed and returned successfully; otherwise, <c>false</c>.</returns>
    public bool TryGetValue(out T? value)
    {
        if (_queue.TryDequeue(out T? item))
        {
            Interlocked.Decrement(ref _currentSize);
            value = item;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Clears all elements from the cache.
    /// </summary>
    public void Clear() => _queue.Clear();
}
