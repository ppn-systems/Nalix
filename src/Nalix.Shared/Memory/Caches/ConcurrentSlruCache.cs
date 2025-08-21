// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Caches;

/// <summary>
/// Provides a high-performance, thread-safe Segmented LRU (SLRU) cache
/// implementation using sharding for scalability.
/// </summary>
/// <typeparam name="TKey">The type of cache keys.</typeparam>
/// <typeparam name="TValue">The type of cache values.</typeparam>
/// <remarks>
/// - The cache is partitioned into multiple shards to reduce contention.
/// - Each shard maintains two segments: probation and protected.
/// - Recently accessed items are promoted to the protected segment,
///   while less frequently accessed items remain in probation.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Count={Count}, Capacity={Capacity}, Hits={Hits}, Misses={Misses}")]
public sealed class ConcurrentSlruCache<TKey, TValue> : System.IDisposable where TKey : notnull
{
    #region Nested

    private sealed class Node
    {
        public TKey Key = default!;
        public TValue? Value;
        public Node? Prev;
        public Node? Next;
        public System.Boolean InProtected; // true => protected, false => probation
        public System.Int64 AccessCount;
        public System.Int64 LastAccessUnixMs;
    }
    private sealed class Shard
    {
        private readonly System.Threading.Lock _lock = new();
        private readonly System.Collections.Generic.Dictionary<TKey, Node> _map;
        private readonly Node _probHead = new();   // probation sentinel
        private readonly Node _probTail = new();
        private readonly Node _protHead = new();   // protected sentinel
        private readonly Node _protTail = new();

        private readonly ConcurrentSlruCache<TKey, TValue> _owner;
        private readonly System.Int32 _probCap;
        private readonly System.Int32 _protCap;

        private System.Int32 _probCount;
        private System.Int32 _protCount;

        public Shard(
            ConcurrentSlruCache<TKey, TValue> owner,
            System.Int32 capacity,
            System.Collections.Generic.IEqualityComparer<TKey>? cmp)
        {
            _owner = owner;
            _map = new System.Collections.Generic.Dictionary<TKey, Node>(capacity, cmp);

            // Mặc định: 1/3 probation, 2/3 protected (có thể tinh chỉnh nếu cần)
            _probCap = System.Math.Max(1, capacity / 3);
            _protCap = System.Math.Max(1, capacity - _probCap);

            // init sentinels
            _probHead.Next = _probTail; _probTail.Prev = _probHead;
            _protHead.Next = _protTail; _protTail.Prev = _protHead;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void Remove(Node n)
        {
            var p = n.Prev!; var nx = n.Next!;
            p.Next = nx; nx.Prev = p;
            n.Prev = n.Next = null;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void AddAfter(Node head, Node n) // push to MRU (head)
        {
            var first = head.Next!;
            n.Next = first; n.Prev = head;
            head.Next = n; first.Prev = n;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static Node Lru(Node tail) => tail.Prev!; // LRU item (before tail)

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static System.Int64 UnixMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public System.Int32 Count
        {
            get
            {
                lock (_lock)
                {
                    return _map.Count;
                }
            }
        }

        public System.Collections.Generic.IEnumerable<TKey> KeysSnapshot()
        {
            lock (_lock)
            {
                var arr = new TKey[_map.Count];
                _map.Keys.CopyTo(arr, 0);
                return arr;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _map.Clear();
                _probHead.Next = _probTail; _probTail.Prev = _probHead;
                _protHead.Next = _protTail; _protTail.Prev = _protHead;
                _probCount = _protCount = 0;
            }
        }

        public System.Boolean ContainsKey(TKey key)
        {
            lock (_lock)
            {
                return _map.ContainsKey(key);
            }
        }

        public System.Boolean Remove(TKey key)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var n))
                {
                    Remove(n);
                    if (n.InProtected)
                    {
                        _protCount--;
                    }
                    else
                    {
                        _probCount--;
                    }

                    _ = _map.Remove(key);
                    return true;
                }
                return false;
            }
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var n))
                {
                    // Update & touch -> Protected.MRU
                    n.Value = value;
                    n.AccessCount++;
                    n.LastAccessUnixMs = UnixMs();

                    Remove(n);
                    if (!n.InProtected) { n.InProtected = true; _probCount--; _protCount++; }
                    AddAfter(_protHead, n);

                    // Evict if protected overflow: demote its LRU to probation
                    if (_protCount > _protCap)
                    {
                        var victim = Lru(_protTail);
                        if (victim != _protHead)
                        {
                            Remove(victim);
                            victim.InProtected = false; _protCount--; _probCount++;
                            AddAfter(_probHead, victim);
                        }
                    }

                    // If probation overflow (after possible demotion), evict probation LRU
                    EvictProbationIfNeeded();
                    _ = System.Threading.Interlocked.Increment(ref _owner._updates);
                    return;
                }

                // New item -> Probation.MRU
                var node = new Node
                {
                    Key = key,
                    Value = value,
                    InProtected = false,
                    AccessCount = 1,
                    LastAccessUnixMs = UnixMs()
                };
                _map[key] = node;
                AddAfter(_probHead, node);
                _probCount++;

                // Evict probation if overflow
                EvictProbationIfNeeded();
                _ = System.Threading.Interlocked.Increment(ref _owner._additions);
            }
        }

        public System.Boolean TryGet(TKey key, out TValue? value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var n))
                {
                    // Hit: promote to Protected.MRU if needed
                    Remove(n);
                    if (!n.InProtected) { n.InProtected = true; _probCount--; _protCount++; }
                    AddAfter(_protHead, n);

                    if (_protCount > _protCap)
                    {
                        var victim = Lru(_protTail);
                        if (victim != _protHead)
                        {
                            Remove(victim);
                            victim.InProtected = false; _protCount--; _probCount++;
                            AddAfter(_probHead, victim);
                        }
                    }

                    EvictProbationIfNeeded();

                    n.AccessCount++;
                    n.LastAccessUnixMs = UnixMs();

                    value = n.Value;
                    _ = System.Threading.Interlocked.Increment(ref _owner._hits);
                    return true;
                }

                value = default;
                _ = System.Threading.Interlocked.Increment(ref _owner._misses);
                return false;
            }
        }

        public TValue GetOrThrow(TKey key)
            => TryGet(key, out var v) ? v!
            : throw new System.Collections.Generic.KeyNotFoundException("The key was not found in the cache.");

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void EvictProbationIfNeeded()
        {
            if (_probCount <= _probCap)
            {
                return;
            }

            var lru = Lru(_probTail);
            if (lru != _probHead)
            {
                Remove(lru);
                _probCount--;
                _ = _map.Remove(lru.Key);
                _ = System.Threading.Interlocked.Increment(ref _owner._evictions);
            }
        }
    }

    #endregion Nested

    #region Fields

    private readonly System.Diagnostics.Stopwatch _uptime = System.Diagnostics.Stopwatch.StartNew();
    private readonly System.Collections.Generic.IEqualityComparer<TKey>? _comparer;
    private readonly Shard[] _shards;
    private readonly System.Int32 _shardMask; // len-1 (power of two)
    private System.Boolean _disposed;

    // global stats 
    private System.Int64 _hits, _misses, _evictions, _additions, _updates;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total capacity of the cache across all shards.
    /// </summary>
    public System.Int32 Capacity { get; }

    /// <summary>
    /// Gets the total number of cache hits since initialization.
    /// </summary>
    public System.Int64 Hits => System.Threading.Interlocked.Read(ref _hits);

    /// <summary>
    /// Gets the total number of cache misses since initialization.
    /// </summary>
    public System.Int64 Misses => System.Threading.Interlocked.Read(ref _misses);

    /// <summary>
    /// Gets the total number of evicted items from the cache.
    /// </summary>
    public System.Int64 Evictions => System.Threading.Interlocked.Read(ref _evictions);

    /// <summary>
    /// Gets the total number of additions performed on the cache.
    /// </summary>
    public System.Int64 Additions => System.Threading.Interlocked.Read(ref _additions);

    /// <summary>
    /// Gets the total number of updates performed on existing cache entries.
    /// </summary>
    public System.Int64 Updates => System.Threading.Interlocked.Read(ref _updates);

    /// <summary>
    /// Gets the ratio of cache hits to total lookups.
    /// </summary>
    public System.Double HitRatio
    {
        get
        {
            var h = Hits; var m = Misses; var t = h + m;
            return t == 0 ? 0 : (System.Double)h / t;
        }
    }

    /// <summary>
    /// Gets the uptime of the cache in milliseconds.
    /// </summary>
    public System.Int64 UptimeMs => _uptime.ElapsedMilliseconds;

    /// <summary>
    /// Gets the total number of items stored across all shards.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    public System.Int32 Count
    {
        get
        {
            System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
            System.Int32 total = 0;
            foreach (var s in _shards)
            {
                total += s.Count;
            }

            return total;
        }
    }

    /// <summary>
    /// Gets a snapshot of all keys currently in the cache.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    public System.Collections.Generic.IEnumerable<TKey> Keys
    {
        get
        {
            System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
            // snapshot từ từng shard để an toàn khi enumerate
            var lists = new System.Collections.Generic.List<TKey[]>(_shards.Length);
            System.Int32 total = 0;
            foreach (var s in _shards)
            {
                var snap = s.KeysSnapshot();
                if (snap is TKey[] arr)
                {
                    lists.Add(arr);
                    total += arr.Length;
                }
                else
                {
                    var a = new System.Collections.Generic.List<TKey>(snap).ToArray();
                    lists.Add(a); total += a.Length;
                }
            }
            var result = new TKey[total];
            System.Int32 ofs = 0;
            foreach (var a in lists)
            {
                System.Array.Copy(a, 0, result, ofs, a.Length);
                ofs += a.Length;
            }
            return result;
        }
    }

    #endregion Properties

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrentSlruCache{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of entries across all shards.</param>
    /// <param name="concurrencyLevel">
    /// The number of shards used internally for concurrency (must be power-of-two).
    /// If <c>null</c>, defaults to the number of processors.
    /// </param>
    /// <param name="comparer">An optional comparer for cache keys.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is less than 1.</exception>
    public ConcurrentSlruCache(
        System.Int32 capacity,
        System.Int32? concurrencyLevel = null,
        System.Collections.Generic.IEqualityComparer<TKey>? comparer = null)
    {
        System.ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _comparer = comparer;
        Capacity = capacity;

        System.Int32 shards = NextPow2(System.Math.Max(1, concurrencyLevel ?? System.Environment.ProcessorCount));
        _shards = new Shard[shards];
        _shardMask = shards - 1;

        // Phân bổ capacity đều cho shards (ít nhất 1 mục/shard)
        System.Int32 baseCap = System.Math.Max(1, capacity / shards);
        System.Int32 remainder = System.Math.Max(0, capacity - (baseCap * shards));

        for (System.Int32 i = 0; i < shards; i++)
        {
            System.Int32 cap = baseCap + (i < remainder ? 1 : 0);
            _shards[i] = new Shard(this, cap, _comparer);
        }
    }

    #endregion Ctor

    #region Public API

    /// <summary>
    /// Adds a new value to the cache, or updates an existing one.
    /// </summary>
    /// <param name="key">The key of the item to add or update.</param>
    /// <param name="value">The value associated with the key.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
        ShardFor(key).AddOrUpdate(key, value);
    }

    /// <summary>
    /// Retrieves a value from the cache for the specified key.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The cached value associated with the key.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the key does not exist.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(TKey key)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
        return ShardFor(key).GetOrThrow(key);
    }

    /// <summary>
    /// Attempts to retrieve a value from the cache.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <param name="value">When this method returns, contains the value if found; otherwise, the default value.</param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryGetValue(TKey key, out TValue? value)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
        return ShardFor(key).TryGet(key, out value);
    }

    /// <summary>
    /// Determines whether the cache contains the specified key.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><c>true</c> if the key exists; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean ContainsKey(TKey key)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
        return ShardFor(key).ContainsKey(key);
    }

    /// <summary>
    /// Removes an item from the cache.
    /// </summary>
    /// <param name="key">The key of the item to remove.</param>
    /// <returns><c>true</c> if the item was removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Remove(TKey key)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
        return ShardFor(key).Remove(key);
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException">Thrown when the cache is disposed.</exception>
    public void Clear()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
        foreach (var s in _shards)
        {
            s.Clear();
        }
    }

    /// <summary>
    /// Resets all statistics counters without affecting the cache items.
    /// </summary>
    public void ResetStatistics()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
        _ = System.Threading.Interlocked.Exchange(ref _hits, 0);
        _ = System.Threading.Interlocked.Exchange(ref _misses, 0);
        _ = System.Threading.Interlocked.Exchange(ref _evictions, 0);
        _ = System.Threading.Interlocked.Exchange(ref _additions, 0);
        _ = System.Threading.Interlocked.Exchange(ref _updates, 0);
        _uptime.Restart();
    }

    /// <summary>
    /// Gets a snapshot of cache statistics.
    /// </summary>
    /// <returns>A dictionary containing statistical information about the cache.</returns>
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetStatistics()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(ConcurrentSlruCache<TKey, TValue>));
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

    #endregion Public API

    #region Disposal

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        System.GC.SuppressFinalize(this);
    }

    #endregion Disposal

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Shard ShardFor(TKey key)
    {
        System.Int32 idx = GetHash(key) & _shardMask;
        return _shards[idx];
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 GetHash(TKey key)
    {
        System.Int32 h = _comparer?.GetHashCode(key) ?? key.GetHashCode();
        unchecked
        {
            System.UInt32 u = (System.UInt32)h;
            u ^= u >> 17; u *= 0xED5AD4BBu;
            u ^= u >> 11; u *= 0xAC4C1B51u;
            u ^= u >> 15; u *= 0x31848BABu;
            u ^= u >> 14;
            return (System.Int32)(u & 0x7FFFFFFF);
        }
    }

    private static System.Int32 NextPow2(System.Int32 v)
    {
        System.UInt32 x = (System.UInt32)(v - 1);
        x |= x >> 1; x |= x >> 2; x |= x >> 4; x |= x >> 8; x |= x >> 16;
        return (System.Int32)(x + 1u);
    }

    #endregion Private Methods
}
