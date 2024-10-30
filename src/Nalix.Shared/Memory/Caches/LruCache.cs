using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Memory.Caches;

/// <summary>
/// High-performance Least Recently Used (LRU) cache optimized for real-time server environments.
/// </summary>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cache value.</typeparam>
public class LruCache<TKey, TValue> : IDisposable where TKey : notnull
{
    #region Nested Types

    // Private storage class for cache items
    private sealed class CacheItem
    {
        public TKey Key { get; init; } = default!;
        public TValue? Value { get; set; }
        public DateTime LastAccessTime { get; set; }
        public Int64 AccessCount { get; set; }
    }

    #endregion Nested Types

    #region Fields

    // Core data structures

    private readonly ReaderWriterLockSlim _cacheLock = new(LockRecursionPolicy.NoRecursion);
    private readonly LinkedList<CacheItem> _usageOrder = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;

    // Caches statistics
    private Int64 _hits;

    private Int64 _misses;
    private Int64 _evictions;
    private Int64 _additions;
    private Int64 _updates;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private Boolean _isDisposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the capacity of the cache.
    /// </summary>
    public Int32 Capacity { get; }

    /// <summary>
    /// Gets the current count of items in the cache.
    /// </summary>
    public Int32 Count
    {
        get
        {
            _cacheLock.EnterReadLock();
            try
            {
                return _cacheMap.Count;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets the TransportProtocol of cache hits.
    /// </summary>
    public Int64 Hits => Interlocked.Read(ref _hits);

    /// <summary>
    /// Gets the TransportProtocol of cache misses.
    /// </summary>
    public Int64 Misses => Interlocked.Read(ref _misses);

    /// <summary>
    /// Gets the hit ratio (hits / total accesses).
    /// </summary>
    public Double HitRatio
    {
        get
        {
            Int64 hits = Hits;
            Int64 misses = Misses;
            Int64 total = hits + misses;
            return total == 0 ? 0 : (Double)hits / total;
        }
    }

    /// <summary>
    /// Gets the TransportProtocol of items evicted from the cache.
    /// </summary>
    public Int64 Evictions => Interlocked.Read(ref _evictions);

    /// <summary>
    /// Gets the TransportProtocol of items added to the cache.
    /// </summary>
    public Int64 Additions => Interlocked.Read(ref _additions);

    /// <summary>
    /// Gets the TransportProtocol of items updated in the cache.
    /// </summary>
    public Int64 Updates => Interlocked.Read(ref _updates);

    /// <summary>
    /// Gets the uptime of the cache in milliseconds.
    /// </summary>
    public Int64 UptimeMs => _uptime.ElapsedMilliseconds;

    /// <summary>
    /// Returns an enumerable collection of the keys in the cache.
    /// </summary>
    /// <returns>An enumerable collection of the keys in the cache.</returns>
    public IEnumerable<TKey> Keys
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));
            _cacheLock.EnterReadLock();
            try
            {
                // Create a copy of keys to avoid modification during enumeration
                TKey[] keys = new TKey[_cacheMap.Count];
                _cacheMap.Keys.CopyTo(keys, 0);
                return keys;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }
    }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LruCache{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="capacity">The maximum TransportProtocol of items in the cache.</param>
    /// <param name="comparer">Optional custom equality comparer for keys.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than 1.</exception>
    public LruCache(Int32 capacity, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");
        }

        Capacity = capacity;
        _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity, comparer);
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Adds or updates an item in the cache.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <param name="value">The value of the item.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterWriteLock();
        try
        {
            if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
            {
                // Update existing item
                _usageOrder.Remove(node);
                node.Value.Value = value;
                node.Value.LastAccessTime = DateTime.UtcNow;
                node.Value.AccessCount++;
                _usageOrder.AddFirst(node);

                _ = Interlocked.Increment(ref _updates);
            }
            else
            {
                // If the cache is full, evict the least recently used item
                if (_cacheMap.Count >= Capacity)
                {
                    var lastNode = _usageOrder.Last;
                    if (lastNode != null)
                    {
                        _usageOrder.RemoveLast();
                        _ = _cacheMap.Remove(lastNode.Value.Key);
                        _ = Interlocked.Increment(ref _evictions);
                    }
                }

                // Push new item
                CacheItem newItem = new()
                {
                    Key = key,
                    Value = value,
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 1
                };

                LinkedListNode<CacheItem> newNode = new(newItem);
                _usageOrder.AddFirst(newNode);
                _cacheMap[key] = newNode;

                _ = Interlocked.Increment(ref _additions);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the item to get.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the cache.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(TKey key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterUpgradeableReadLock();
        try
        {
            if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
            {
                // Move the accessed node to the front
                _cacheLock.EnterWriteLock();
                try
                {
                    _usageOrder.Remove(node);
                    _usageOrder.AddFirst(node);
                    node.Value.LastAccessTime = DateTime.UtcNow;
                    node.Value.AccessCount++;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }

                _ = Interlocked.Increment(ref _hits);
                return node.Value.Value!;
            }

            _ = Interlocked.Increment(ref _misses);
            throw new KeyNotFoundException("The key was not found in the cache.");
        }
        finally
        {
            _cacheLock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Tries to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the item to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.</param>
    /// <returns>true if the key was found in the cache; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Boolean TryGetValue(TKey key, out TValue? value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterUpgradeableReadLock();
        try
        {
            if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
            {
                // Move the accessed node to the front
                _cacheLock.EnterWriteLock();
                try
                {
                    _usageOrder.Remove(node);
                    _usageOrder.AddFirst(node);
                    node.Value.LastAccessTime = DateTime.UtcNow;
                    node.Value.AccessCount++;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }

                value = node.Value.Value;
                _ = Interlocked.Increment(ref _hits);
                return true;
            }

            value = default;
            _ = Interlocked.Increment(ref _misses);
            return false;
        }
        finally
        {
            _cacheLock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Determines whether the cache contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the cache.</param>
    /// <returns>true if the cache contains an element with the specified key; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Boolean ContainsKey(TKey key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterReadLock();
        try
        {
            return _cacheMap.ContainsKey(key);
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes the value with the specified key from the cache.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Boolean Remove(TKey key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterWriteLock();
        try
        {
            if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
            {
                _usageOrder.Remove(node);
                return _cacheMap.Remove(key);
            }
            return false;
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets information about a cache item without updating its position in the LRU order.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <returns>A tuple containing (item exists, access count, last access time).</returns>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (Boolean Exists, Int64 AccessCount, DateTime LastAccessTime) GetItemInfo(TKey key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterReadLock();
        try
        {
            if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
            {
                return (true, node.Value.AccessCount, node.Value.LastAccessTime);
            }
            return (false, 0, DateTime.MinValue);
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterWriteLock();
        try
        {
            _cacheMap.Clear();
            _usageOrder.Clear();
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Resets the cache statistics without clearing the cache items.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _ = Interlocked.Exchange(ref _hits, 0);
        _ = Interlocked.Exchange(ref _misses, 0);
        _ = Interlocked.Exchange(ref _evictions, 0);
        _ = Interlocked.Exchange(ref _additions, 0);
        _ = Interlocked.Exchange(ref _updates, 0);
        _uptime.Restart();
    }

    /// <summary>
    /// Gets a snapshot of the current cache statistics.
    /// </summary>
    /// <returns>A dictionary containing cache statistics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<String, Object> GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        return new Dictionary<String, Object>
        {
            ["Capacity"] = Capacity,
            ["Count"] = Count,
            ["Hits"] = Hits,
            ["Misses"] = Misses,
            ["HitRatio"] = HitRatio,
            ["Evictions"] = Evictions,
            ["Additions"] = Additions,
            ["Updates"] = Updates,
            ["UptimeMs"] = UptimeMs
        };
    }

    #endregion Public Methods

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
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
