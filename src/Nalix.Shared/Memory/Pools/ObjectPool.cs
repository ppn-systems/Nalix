using Nalix.Common.Caching;
using Nalix.Shared.Memory.PoolTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Memory.Pools;

/// <summary>
/// A high-performance thread-safe pool for storing and reusing <see cref="IPoolable"/> instances
/// in real-time server environments.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ObjectPool"/> class with the specified maximum items per type.
/// </remarks>
/// <param name="defaultMaxItemsPerType">The default maximum ProtocolType of items to store per type.</param>
public sealed class ObjectPool(Int32 defaultMaxItemsPerType)
{
    #region Constants

    /// <summary>
    /// Standard maximum pool size to prevent unbounded memory growth
    /// </summary>
    public const Int32 DefaultMaxSize = 1024;

    #endregion Constants

    #region Fields

    // Type-specific storage for pooled objects
    private readonly ConcurrentDictionary<Type, TypePool> _typePools = new();

    // Statistics tracking
    private Int64 _totalCreated;

    private Int64 _totalReturned;
    private Int64 _totalRented;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    // Configuration
    private readonly Int32 _defaultMaxItemsPerType = defaultMaxItemsPerType > 0 ? defaultMaxItemsPerType : DefaultMaxSize;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the singleton instance of the object pool.
    /// </summary>
    public static ObjectPool Default { get; } = new();

    /// <summary>
    /// Event for trace information.
    /// </summary>
    public event Action<String>? TraceOccurred;

    /// <summary>
    /// Gets the total ProtocolType of objects created across all types.
    /// </summary>
    public Int64 TotalCreatedCount => Interlocked.Read(ref _totalCreated);

    /// <summary>
    /// Gets the total ProtocolType of currently pooled objects across all types.
    /// </summary>
    public Int32 TotalAvailableCount
    {
        get
        {
            Int32 count = 0;
            foreach (var pool in _typePools.Values)
            {
                count += pool.AvailableCount;
            }
            return count;
        }
    }

    /// <summary>
    /// Gets the ProtocolType of object types currently being pooled.
    /// </summary>
    public Int32 TypeCount => _typePools.Count;

    /// <summary>
    /// Gets the total ProtocolType of objects returned to the pool.
    /// </summary>
    public Int64 TotalReturnedCount => Interlocked.Read(ref _totalReturned);

    /// <summary>
    /// Gets the total ProtocolType of objects rented from the pool.
    /// </summary>
    public Int64 TotalRentedCount => Interlocked.Read(ref _totalRented);

    /// <summary>
    /// Gets the pool uptime in milliseconds.
    /// </summary>
    public Int64 UptimeMs => _uptime.ElapsedMilliseconds;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPool"/> class with default settings.
    /// </summary>
    public ObjectPool() : this(DefaultMaxSize)
    {
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets or creates and returns an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of object to get from the pool.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);

        // Get or create the type-specific pool
        var typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        // Try to get an object from the pool or create a new one
        if (typePool.TryPop(out IPoolable? obj) && obj != null)
        {
            _ = Interlocked.Increment(ref _totalRented);
            return (T)obj;
        }

        // Create a new instance if the pool is empty
        T newObj = new();

        _ = Interlocked.Increment(ref _totalCreated);
        _ = Interlocked.Increment(ref _totalRented);

        TraceOccurred?.Invoke($"Get<{type.Name}>: Created new instance (TotalCreated={TotalCreatedCount})");

        return newObj;
    }

    /// <summary>
    /// Returns an instance of <typeparamref name="T"/> to the pool for future reuse.
    /// </summary>
    /// <typeparam name="T">The type of object to return to the pool.</typeparam>
    /// <param name="obj">The object to return to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return<T>(T obj) where T : IPoolable, new()
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        Type type = typeof(T);

        // Reset the object before returning it to the pool
        obj.ResetForPool();

        // Get or create the type-specific pool
        var typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        // Try to add the object to the pool
        if (typePool.TryPush(obj))
        {
            _ = Interlocked.Increment(ref _totalReturned);
            return;
        }

        // If the pool is full, the object will be garbage collected
        TraceOccurred?.Invoke($"Return<{type.Name}>: Pools at capacity, object discarded");
    }

    /// <summary>
    /// Creates and adds multiple new instances of <typeparamref name="T"/> to the pool.
    /// </summary>
    /// <typeparam name="T">The type of objects to preallocate.</typeparam>
    /// <param name="count">The ProtocolType of instances to preallocate.</param>
    /// <returns>The ProtocolType of instances successfully preallocated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Int32 Prealloc<T>(Int32 count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            return 0;
        }

        Type type = typeof(T);
        var typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        Int32 created = 0;
        for (Int32 i = 0; i < count; i++)
        {
            // Create a new instance and try to add it to the pool
            T obj = new();
            if (typePool.TryPush(obj))
            {
                created++;
                _ = Interlocked.Increment(ref _totalCreated);
                _ = Interlocked.Increment(ref _totalReturned);
            }
            else
            {
                // If the pool is full, stop creating objects
                break;
            }
        }

        if (created > 0)
        {
            TraceOccurred?.Invoke($"Prealloc<{type.Name}>: Added {created} instances to pool");
        }

        return created;
    }

    /// <summary>
    /// Sets the maximum capacity for a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to configure.</typeparam>
    /// <param name="maxCapacity">The maximum capacity for the type's pool.</param>
    /// <returns>True if the capacity was set, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Boolean SetMaxCapacity<T>(Int32 maxCapacity) where T : IPoolable
    {
        if (maxCapacity < 0)
        {
            return false;
        }

        Type type = typeof(T);
        if (_typePools.TryGetValue(type, out TypePool? typePool))
        {
            typePool.SetMaxCapacity(maxCapacity);
            TraceOccurred?.Invoke($"SetMaxCapacity<{type.Name}>: Set to {maxCapacity}");
            return true;
        }

        // Create a new pool with the specified capacity
        _typePools[type] = new TypePool(maxCapacity);
        TraceOccurred?.Invoke($"SetMaxCapacity<{type.Name}>: Created new pool with capacity {maxCapacity}");
        return true;
    }

    /// <summary>
    /// Gets information about a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to get information about.</typeparam>
    /// <returns>A dictionary containing pool statistics for the type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<String, Object> GetTypeInfo<T>() where T : IPoolable
    {
        Type type = typeof(T);
        return _typePools.TryGetValue(type, out TypePool? typePool)
            ? new Dictionary<String, Object>
            {
                ["TypeName"] = type.Name,
                ["AvailableCount"] = typePool.AvailableCount,
                ["MaxCapacity"] = typePool.MaxCapacity,
                ["IsActive"] = true
            }
            : new Dictionary<String, Object>
            {
                ["TypeName"] = type.Name,
                ["AvailableCount"] = 0,
                ["MaxCapacity"] = _defaultMaxItemsPerType,
                ["IsActive"] = false
            };
    }

    /// <summary>
    /// Gets statistics about the pool's usage.
    /// </summary>
    /// <returns>A dictionary containing statistics about the pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<String, Object> GetStatistics()
    {
        return new Dictionary<String, Object>
        {
            ["TotalCreatedCount"] = TotalCreatedCount,
            ["TotalAvailableCount"] = TotalAvailableCount,
            ["TypeCount"] = TypeCount,
            ["TotalRentedCount"] = TotalRentedCount,
            ["TotalReturnedCount"] = TotalReturnedCount,
            ["ActiveRentals"] = TotalRentedCount - TotalReturnedCount,
            ["UptimeMs"] = UptimeMs,
            ["DefaultMaxItemsPerType"] = _defaultMaxItemsPerType
        };
    }

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    /// <returns>The total ProtocolType of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Int32 Clear()
    {
        Int32 removedCount = 0;
        foreach (var pool in _typePools.Values)
        {
            removedCount += pool.Clear();
        }

        TraceOccurred?.Invoke($"Dispose: Removed {removedCount} objects from all pools");
        return removedCount;
    }

    /// <summary>
    /// Clears a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to clear from the pool.</typeparam>
    /// <returns>The ProtocolType of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Int32 ClearType<T>() where T : IPoolable
    {
        Type type = typeof(T);
        if (_typePools.TryGetValue(type, out TypePool? typePool))
        {
            Int32 removedCount = typePool.Clear();
            TraceOccurred?.Invoke($"ClearType<{type.Name}>: Removed {removedCount} objects");
            return removedCount;
        }

        return 0;
    }

    /// <summary>
    /// Trims all type pools to their target sizes.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to trim to (0-100).</param>
    /// <returns>The total ProtocolType of objects removed.</returns>
    public Int32 Trim(Int32 percentage = 50)
    {
        if (percentage < 0)
        {
            percentage = 0;
        }

        if (percentage > 100)
        {
            percentage = 100;
        }

        Int32 removedCount = 0;
        foreach (var pool in _typePools.Values)
        {
            removedCount += pool.Trim(percentage);
        }

        if (removedCount > 0)
        {
            TraceOccurred?.Invoke($"Trim({percentage}%): Removed {removedCount} objects from all pools");
        }

        return removedCount;
    }

    /// <summary>
    /// Resets the statistics of the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        _ = Interlocked.Exchange(ref _totalCreated, 0);
        _ = Interlocked.Exchange(ref _totalRented, 0);
        _ = Interlocked.Exchange(ref _totalReturned, 0);
        _uptime.Restart();

        TraceOccurred?.Invoke("ResetStatistics: Pools statistics reset");
    }

    /// <summary>
    /// Gets information about all type pools.
    /// </summary>
    /// <returns>A list of dictionaries containing information about each type pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<Dictionary<String, Object>> GetAllTypeInfo()
    {
        List<Dictionary<String, Object>> result = [];

        foreach (var kvp in _typePools)
        {
            var typePool = kvp.Value;
            result.Add(new Dictionary<String, Object>
            {
                ["TypeName"] = kvp.Key.Name,
                ["AvailableCount"] = typePool.AvailableCount,
                ["MaxCapacity"] = typePool.MaxCapacity,
                ["IsActive"] = true
            });
        }

        return result;
    }

    /// <summary>
    /// Batch returns multiple objects to the pool.
    /// </summary>
    /// <typeparam name="T">The type of objects to return.</typeparam>
    /// <param name="objects">The objects to return to the pool.</param>
    /// <returns>The ProtocolType of objects successfully returned to the pool.</returns>
    /// <exception cref="ArgumentNullException">Thrown when objects is null.</exception>
    public Int32 ReturnMultiple<T>(IEnumerable<T> objects) where T : IPoolable, new()
    {
        ArgumentNullException.ThrowIfNull(objects);

        Int32 returnedCount = 0;
        Type type = typeof(T);
        var typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        foreach (var obj in objects)
        {
            if (obj == null)
            {
                continue;
            }

            obj.ResetForPool();

            if (typePool.TryPush(obj))
            {
                returnedCount++;
                _ = Interlocked.Increment(ref _totalReturned);
            }
        }

        if (returnedCount > 0)
        {
            TraceOccurred?.Invoke($"ReturnMultiple<{type.Name}>: Returned {returnedCount} objects to pool");
        }

        return returnedCount;
    }

    /// <summary>
    /// Gets multiple objects from the pool at once.
    /// </summary>
    /// <typeparam name="T">The type of objects to get.</typeparam>
    /// <param name="count">The ProtocolType of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    /// <exception cref="ArgumentException">Thrown when count is less than or equal to zero.</exception>
    public List<T> GetMultiple<T>(Int32 count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        var result = new List<T>(count);
        Type type = typeof(T);

        for (Int32 i = 0; i < count; i++)
        {
            result.Add(Get<T>());
        }

        TraceOccurred?.Invoke($"GetMultiple<{type.Name}>: Got {count} objects from pool");

        return result;
    }

    /// <summary>
    /// Creates a new type-specific pool adapter for more efficient operations with a specific type.
    /// </summary>
    /// <typeparam name="T">The type of objects to manage in the pool.</typeparam>
    /// <returns>A type-specific pool adapter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypedObjectPool<T> CreateTypedPool<T>() where T : IPoolable, new() => new(this);

    #endregion Public Methods
}
