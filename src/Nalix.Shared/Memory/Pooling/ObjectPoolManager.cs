using Nalix.Common.Caching;
using Nalix.Shared.Injection.DI;
using Nalix.Shared.Memory.Pools;
using Nalix.Shared.Memory.PoolTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Shared.Memory.Pooling;

/// <summary>
/// Provides thread-safe access to a collection of object pools containing instances of <see cref="IPoolable"/>.
/// </summary>
public sealed class ObjectPoolManager : SingletonBase<ObjectPoolManager>
{
    #region Fields

    // Thread-safe storage for pools
    private readonly ConcurrentDictionary<Type, ObjectPool> _poolDict = new();

    // Configuration
    private Int32 _defaultMaxPoolSize = 1024;

    // Statistics tracking
    internal Int64 _totalGetOperations;

    internal Int64 _totalReturnOperations;
    internal DateTime _startTime = DateTime.UtcNow;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the default maximum size for new pools.
    /// </summary>
    public Int32 DefaultMaxPoolSize
    {
        get => _defaultMaxPoolSize;
        set => _defaultMaxPoolSize = value > 0 ? value : 1024;
    }

    /// <summary>
    /// Gets the total Number of pools currently managed.
    /// </summary>
    public Int32 PoolCount => _poolDict.Count;

    /// <summary>
    /// Gets the total Number of get operations performed.
    /// </summary>
    public Int64 TotalGetOperations => Interlocked.Read(ref _totalGetOperations);

    /// <summary>
    /// Gets the total Number of return operations performed.
    /// </summary>
    public Int64 TotalReturnOperations => Interlocked.Read(ref _totalReturnOperations);

    /// <summary>
    /// Gets the uptime of the pool manager.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - _startTime;

    #endregion Properties

    #region Constructor

    private ObjectPoolManager()
    {
        // Private constructor for singleton pattern
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Gets or creates and returns an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of object to get from the pool.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : IPoolable, new()
    {
        _ = Interlocked.Increment(ref _totalGetOperations);
        ObjectPool pool = GetOrCreatePool<T>();
        return pool.Get<T>();
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

        _ = Interlocked.Increment(ref _totalReturnOperations);
        ObjectPool pool = GetOrCreatePool<T>();
        pool.Return(obj);
    }

    /// <summary>
    /// Gets or creates a type-specific pool adapter for more efficient operations with a specific type.
    /// </summary>
    /// <typeparam name="T">The type of objects to manage in the pool.</typeparam>
    /// <returns>A type-specific pool adapter.</returns>
    public TypedObjectPoolAdapter<T> GetTypedPool<T>() where T : IPoolable, new()
    {
        ObjectPool pool = GetOrCreatePool<T>();
        return new TypedObjectPoolAdapter<T>(pool, this);
    }

    /// <summary>
    /// Creates and adds multiple new instances of <typeparamref name="T"/> to the pool.
    /// </summary>
    /// <typeparam name="T">The type of objects to preallocate.</typeparam>
    /// <param name="count">The Number of instances to preallocate.</param>
    /// <returns>The Number of instances successfully preallocated.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is less than or equal to zero.</exception>
    public Int32 Prealloc<T>(Int32 count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        ObjectPool pool = GetOrCreatePool<T>();
        return pool.Prealloc<T>(count);
    }

    /// <summary>
    /// Sets the maximum capacity for a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to configure.</typeparam>
    /// <param name="maxCapacity">The maximum capacity for the type's pool.</param>
    /// <returns>True if the capacity was set, false otherwise.</returns>
    public Boolean SetMaxCapacity<T>(Int32 maxCapacity) where T : IPoolable
    {
        if (maxCapacity < 0)
        {
            return false;
        }

        Type type = typeof(T);
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            return pool.SetMaxCapacity<T>(maxCapacity);
        }

        // Create a new pool with the specified capacity
        pool = new ObjectPool(maxCapacity);
        _poolDict[type] = pool;
        return true;
    }

    /// <summary>
    /// Gets information about a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to get information about.</typeparam>
    /// <returns>A dictionary containing pool statistics for the type.</returns>
    public Dictionary<String, Object> GetTypeInfo<T>() where T : IPoolable
    {
        Type type = typeof(T);
        return _poolDict.TryGetValue(type, out ObjectPool? pool)
            ? pool.GetTypeInfo<T>()
            : new Dictionary<String, Object>
            {
                ["TypeName"] = type.Name,
                ["AvailableCount"] = 0,
                ["MaxCapacity"] = _defaultMaxPoolSize,
                ["IsActive"] = false
            };
    }

    /// <summary>
    /// Clears all objects from a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to clear from the pool.</typeparam>
    /// <returns>The Number of objects removed.</returns>
    public Int32 ClearPool<T>() where T : IPoolable
    {
        Type type = typeof(T);
        return _poolDict.TryGetValue(type, out ObjectPool? pool) ? pool.ClearType<T>() : 0;
    }

    /// <summary>
    /// Clears all objects from all pools.
    /// </summary>
    /// <returns>The total Number of objects removed.</returns>
    public Int32 ClearAllPools()
    {
        Int32 totalRemoved = 0;

        foreach (var pool in _poolDict.Values)
        {
            totalRemoved += pool.Clear();
        }

        return totalRemoved;
    }

    /// <summary>
    /// Trims all pools to their target sizes.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <returns>The total Number of objects removed.</returns>
    public Int32 TrimAllPools(Int32 percentage = 50)
    {
        Int32 totalRemoved = 0;

        foreach (var pool in _poolDict.Values)
        {
            totalRemoved += pool.Trim(percentage);
        }

        return totalRemoved;
    }

    /// <summary>
    /// Reset all statistics for the pool manager.
    /// </summary>
    public void ResetStatistics()
    {
        _ = Interlocked.Exchange(ref _totalGetOperations, 0);
        _ = Interlocked.Exchange(ref _totalReturnOperations, 0);
        _startTime = DateTime.UtcNow;

        // Also reset statistics for all pools
        foreach (var pool in _poolDict.Values)
        {
            pool.ResetStatistics();
        }
    }

    /// <summary>
    /// Schedules a regular trimming operation to run in the background.
    /// </summary>
    /// <param name="interval">The interval between trim operations.</param>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <param name="cancellationToken">A token to cancel the background trimming.</param>
    /// <returns>A task representing the background trimming operation.</returns>
    public Task ScheduleRegularTrimming(TimeSpan interval, Int32 percentage = 50, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);
                    _ = TrimAllPools(percentage);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception)
                {
                    // Log or handle other exceptions
                    // Continue the loop to maintain the trimming schedule
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Generates a report on the current state of all pools.
    /// </summary>
    /// <returns>A string containing the report.</returns>
    public String GenerateReport()
    {
        StringBuilder sb = new();
        _ = sb.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ObjectPoolManager Status:");
        _ = sb.AppendLine($"Total Pools: {PoolCount}");
        _ = sb.AppendLine($"Total Get Operations: {TotalGetOperations}");
        _ = sb.AppendLine($"Total Return Operations: {TotalReturnOperations}");
        _ = sb.AppendLine($"Uptime: {Uptime.TotalHours:F2} hours");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Pool Details:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("Type                     | Available | Max Capacity | Created");
        _ = sb.AppendLine("------------------------------------------------------------");

        // Sort pools by type name for better readability
        var sortedPools = new List<KeyValuePair<Type, ObjectPool>>(_poolDict);
        sortedPools.Sort((a, b) => String.Compare(a.Key.Name, b.Key.Name, StringComparison.Ordinal));

        foreach (var kvp in sortedPools)
        {
            Type type = kvp.Key;
            ObjectPool pool = kvp.Value;
            var stats = pool.GetStatistics();
            var typeStats = pool.GetTypeInfo<IPoolable>();

            String typeName = type.Name.Length > 24
                ? String.Concat(type.Name.AsSpan(0, 21), "...")
                : type.Name.PadRight(24);

            Int32 availableCount = Convert.ToInt32(typeStats["AvailableCount"]);
            Int32 maxCapacity = Convert.ToInt32(typeStats["MaxCapacity"]);
            Int64 created = Convert.ToInt64(stats["TotalCreatedCount"]);

            _ = sb.AppendLine($"{typeName} | {availableCount,9} | {maxCapacity,12} | {created,7}");
        }

        _ = sb.AppendLine("------------------------------------------------------------");
        return sb.ToString();
    }

    /// <summary>
    /// Gets detailed statistics for all pools.
    /// </summary>
    /// <returns>A dictionary containing statistics for the pool manager and all pools.</returns>
    public Dictionary<String, Object> GetDetailedStatistics()
    {
        var stats = new Dictionary<String, Object>
        {
            ["PoolCount"] = PoolCount,
            ["TotalGetOperations"] = TotalGetOperations,
            ["TotalReturnOperations"] = TotalReturnOperations,
            ["UptimeSeconds"] = Uptime.TotalSeconds,
            ["StartTime"] = _startTime,
            ["DefaultMaxPoolSize"] = DefaultMaxPoolSize
        };

        var poolStats = new Dictionary<String, Dictionary<String, Object>>();

        foreach (var kvp in _poolDict)
        {
            String typeName = kvp.Key.Name;
            poolStats[typeName] = kvp.Value.GetStatistics();
        }

        stats["Pools"] = poolStats;

        return stats;
    }

    /// <summary>
    /// Gets or creates an <see cref="ObjectPool"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of objects the pool will contain.</typeparam>
    /// <returns>An object pool for the specified type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ObjectPool GetOrCreatePool<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);
        return _poolDict.GetOrAdd(type, _ => new ObjectPool(_defaultMaxPoolSize));
    }

    #endregion APIs
}