// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Caches;

/// <summary>
/// High-performance Least Recently Used (LRU) cache optimized for real-time server environments.
/// </summary>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cache value.</typeparam>
[System.Diagnostics.DebuggerDisplay("Count={Count}, Capacity={Capacity}, Hits={Hits}, Misses={Misses}")]
public class LruCache<TKey, TValue> : System.IDisposable where TKey : notnull
{
    #region Nested Types

    // Private storage class for cache items
    private sealed class CacheItem
    {
        public TKey Key { get; init; } = default!;
        public TValue? Value { get; set; }
        public System.Int64 AccessCount { get; set; }
        public System.DateTime LastAccessTime { get; set; }
    }

    #endregion Nested Types

    #region Fields

    // Core data structures
    private readonly System.Diagnostics.Stopwatch _uptime = System.Diagnostics.Stopwatch.StartNew();
    private readonly System.Threading.ReaderWriterLockSlim _cacheLock;
    private readonly System.Collections.Generic.LinkedList<CacheItem> _usageOrder = new();
    private readonly System.Collections.Generic.Dictionary<
        TKey, System.Collections.Generic.LinkedListNode<CacheItem>> _cacheMap;

    // Caches statistics
    private System.Int64 _hits;
    private System.Int64 _misses;
    private System.Int64 _evictions;
    private System.Int64 _additions;
    private System.Int64 _updates;
    private System.Boolean _isDisposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the capacity of the cache.
    /// </summary>
    public System.Int32 Capacity { get; }

    /// <summary>
    /// Gets the current count of items in the cache.
    /// </summary>
    public System.Int32 Count
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
    /// Gets the ProtocolType of cache hits.
    /// </summary>
    public System.Int64 Hits => System.Threading.Interlocked.Read(ref _hits);

    /// <summary>
    /// Gets the ProtocolType of cache misses.
    /// </summary>
    public System.Int64 Misses => System.Threading.Interlocked.Read(ref _misses);

    /// <summary>
    /// Gets the hit ratio (hits / total accesses).
    /// </summary>
    public System.Double HitRatio
    {
        get
        {
            System.Int64 hits = Hits;
            System.Int64 misses = Misses;
            System.Int64 total = hits + misses;
            return total == 0 ? 0 : (System.Double)hits / total;
        }
    }

    /// <summary>
    /// Gets the ProtocolType of items evicted from the cache.
    /// </summary>
    public System.Int64 Evictions => System.Threading.Interlocked.Read(ref _evictions);

    /// <summary>
    /// Gets the ProtocolType of items added to the cache.
    /// </summary>
    public System.Int64 Additions => System.Threading.Interlocked.Read(ref _additions);

    /// <summary>
    /// Gets the ProtocolType of items updated in the cache.
    /// </summary>
    public System.Int64 Updates => System.Threading.Interlocked.Read(ref _updates);

    /// <summary>
    /// Gets the uptime of the cache in milliseconds.
    /// </summary>
    public System.Int64 UptimeMs => _uptime.ElapsedMilliseconds;

    /// <summary>
    /// Returns an enumerable collection of the keys in the cache.
    /// </summary>
    /// <returns>An enumerable collection of the keys in the cache.</returns>
    public System.Collections.Generic.IEnumerable<TKey> Keys
    {
        get
        {
            System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));
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
    /// <param name="capacity">The maximum ProtocolType of items in the cache.</param>
    /// <param name="comparer">Optional custom equality comparer for keys.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when capacity is less than 1.</exception>
    public LruCache(System.Int32 capacity,
        System.Collections.Generic.IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");
        }

        this.Capacity = capacity;

        _cacheMap = new(capacity, comparer);
        _uptime = System.Diagnostics.Stopwatch.StartNew();
        _cacheLock = new(System.Threading.LockRecursionPolicy.NoRecursion);
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Adds or updates an item in the cache.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <param name="value">The value of the item.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterWriteLock();
        try
        {
            if (_cacheMap.TryGetValue(key,
                out System.Collections.Generic.LinkedListNode<CacheItem>? node))
            {
                // Update existing item
                _usageOrder.Remove(node);
                node.Value.Value = value;
                node.Value.LastAccessTime = System.DateTime.UtcNow;
                node.Value.AccessCount++;
                _usageOrder.AddFirst(node);

                _ = System.Threading.Interlocked.Increment(ref _updates);
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
                        _ = System.Threading.Interlocked.Increment(ref _evictions);
                    }
                }

                // Push new item
                CacheItem newItem = new()
                {
                    Key = key,
                    Value = value,
                    LastAccessTime = System.DateTime.UtcNow,
                    AccessCount = 1
                };

                System.Collections.Generic.LinkedListNode<CacheItem> newNode = new(newItem);
                _usageOrder.AddFirst(newNode);
                _cacheMap[key] = newNode;

                _ = System.Threading.Interlocked.Increment(ref _additions);
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
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the key is not found in the cache.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(TKey key)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterUpgradeableReadLock();
        try
        {
            if (_cacheMap.TryGetValue(key,
                out System.Collections.Generic.LinkedListNode<CacheItem>? node))
            {
                // Move the accessed node to the front
                _cacheLock.EnterWriteLock();
                try
                {
                    _usageOrder.Remove(node);
                    _usageOrder.AddFirst(node);
                    node.Value.LastAccessTime = System.DateTime.UtcNow;
                    node.Value.AccessCount++;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }

                _ = System.Threading.Interlocked.Increment(ref _hits);
                return node.Value.Value!;
            }

            _ = System.Threading.Interlocked.Increment(ref _misses);
            throw new System.Collections.Generic.KeyNotFoundException("The key was not found in the cache.");
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
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryGetValue(TKey key, out TValue? value)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterUpgradeableReadLock();
        try
        {
            if (_cacheMap.TryGetValue(key,
                out System.Collections.Generic.LinkedListNode<CacheItem>? node))
            {
                // Move the accessed node to the front
                _cacheLock.EnterWriteLock();
                try
                {
                    _usageOrder.Remove(node);
                    _usageOrder.AddFirst(node);
                    node.Value.LastAccessTime = System.DateTime.UtcNow;
                    node.Value.AccessCount++;
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }

                value = node.Value.Value;
                _ = System.Threading.Interlocked.Increment(ref _hits);
                return true;
            }

            value = default;
            _ = System.Threading.Interlocked.Increment(ref _misses);
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
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean ContainsKey(TKey key)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

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
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Remove(TKey key)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterWriteLock();
        try
        {
            if (_cacheMap.TryGetValue(key,
                out System.Collections.Generic.LinkedListNode<CacheItem>? node))
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
    /// <exception cref="System.ArgumentNullException">Thrown when key is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public (System.Boolean Exists, System.Int64 AccessCount, System.DateTime LastAccessTime) GetItemInfo(TKey key)
    {
        if (key == null)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _cacheLock.EnterReadLock();
        try
        {
            if (_cacheMap.TryGetValue(key,
                out System.Collections.Generic.LinkedListNode<CacheItem>? node))
            {
                return (true, node.Value.AccessCount, node.Value.LastAccessTime);
            }
            return (false, 0, System.DateTime.MinValue);
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
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        _ = System.Threading.Interlocked.Exchange(ref _hits, 0);
        _ = System.Threading.Interlocked.Exchange(ref _misses, 0);
        _ = System.Threading.Interlocked.Exchange(ref _evictions, 0);
        _ = System.Threading.Interlocked.Exchange(ref _additions, 0);
        _ = System.Threading.Interlocked.Exchange(ref _updates, 0);
        _uptime.Restart();
    }

    /// <summary>
    /// Gets a snapshot of the current cache statistics.
    /// </summary>
    /// <returns>A dictionary containing cache statistics.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetStatistics()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, nameof(LruCache<TKey, TValue>));

        return new System.Collections.Generic.Dictionary<System.String, System.Object>
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

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
