// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pools;
using Nalix.Shared.Memory.PoolTypes;

namespace Nalix.Shared.Memory.Pooling;

/// <summary>
/// Provides thread-safe access to a collection of object pools containing instances of <see cref="IPoolable"/>.
/// </summary>
public sealed class ObjectPoolManager : IReportable
{
    #region Fields

    // Thread-safe storage for pools
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, ObjectPool> _poolDict = new();

    // Configuration
    private System.Int32 _defaultMaxPoolSize = 1024;

    // Statistics tracking
    internal System.Int64 _totalGetOperations;

    internal System.Int64 _totalReturnOperations;
    internal System.DateTime _startTime = System.DateTime.UtcNow;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the default maximum size for new pools.
    /// </summary>
    public System.Int32 DefaultMaxPoolSize
    {
        get => _defaultMaxPoolSize;
        set => _defaultMaxPoolSize = value > 0 ? value : 1024;
    }

    /// <summary>
    /// Gets the total ProtocolType of pools currently managed.
    /// </summary>
    public System.Int32 PoolCount => _poolDict.Count;

    /// <summary>
    /// Gets the total ProtocolType of get operations performed.
    /// </summary>
    public System.Int64 TotalGetOperations => System.Threading.Interlocked.Read(ref _totalGetOperations);

    /// <summary>
    /// Gets the total ProtocolType of return operations performed.
    /// </summary>
    public System.Int64 TotalReturnOperations => System.Threading.Interlocked.Read(ref _totalReturnOperations);

    /// <summary>
    /// Gets the uptime of the pool manager.
    /// </summary>
    public System.TimeSpan Uptime => System.DateTime.UtcNow - _startTime;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPoolManager"/> class.
    /// </summary>
    public ObjectPoolManager()
    {
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Gets or creates and returns an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of object to get from the pool.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public T Get<T>() where T : IPoolable, new()
    {
        _ = System.Threading.Interlocked.Increment(ref _totalGetOperations);
        ObjectPool pool = GetOrCreatePool<T>();
        return pool.Get<T>();
    }

    /// <summary>
    /// Returns an instance of <typeparamref name="T"/> to the pool for future reuse.
    /// </summary>
    /// <typeparam name="T">The type of object to return to the pool.</typeparam>
    /// <param name="obj">The object to return to the pool.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when obj is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return<T>([System.Diagnostics.CodeAnalysis.NotNull] T obj) where T : IPoolable, new()
    {
        if (obj == null)
        {
            throw new System.ArgumentNullException(nameof(obj));
        }

        _ = System.Threading.Interlocked.Increment(ref _totalReturnOperations);
        ObjectPool pool = GetOrCreatePool<T>();
        pool.Return(obj);
    }

    /// <summary>
    /// Gets or creates a type-specific pool adapter for more efficient operations with a specific type.
    /// </summary>
    /// <typeparam name="T">The type of objects to manage in the pool.</typeparam>
    /// <returns>A type-specific pool adapter.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public TypedObjectPoolAdapter<T> GetTypedPool<T>() where T : IPoolable, new()
    {
        ObjectPool pool = GetOrCreatePool<T>();
        return new TypedObjectPoolAdapter<T>(pool, this);
    }

    /// <summary>
    /// Creates and adds multiple new instances of <typeparamref name="T"/> to the pool.
    /// </summary>
    /// <typeparam name="T">The type of objects to preallocate.</typeparam>
    /// <param name="count">The ProtocolType of instances to preallocate.</param>
    /// <returns>The ProtocolType of instances successfully preallocated.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when count is less than or equal to zero.</exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Int32 Prealloc<T>([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        ObjectPool pool = GetOrCreatePool<T>();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[SH.{nameof(ObjectPoolManager)}:{nameof(Prealloc)}] prealloc type={typeof(T).Name} count={count}");

        return pool.Prealloc<T>(count);
    }

    /// <summary>
    /// Sets the maximum capacity for a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to configure.</typeparam>
    /// <param name="maxCapacity">The maximum capacity for the type's pool.</param>
    /// <returns>True if the capacity was set, false otherwise.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Boolean SetMaxCapacity<T>([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 maxCapacity) where T : IPoolable
    {
        if (maxCapacity < 0)
        {
            return false;
        }

        System.Type type = typeof(T);
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            return pool.SetMaxCapacity<T>(maxCapacity);
        }

        // Create a new pool with the specified capacity
        pool = new ObjectPool(maxCapacity);
        _poolDict[type] = pool;

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                        .Info($"[SH.{nameof(ObjectPoolManager)}:{nameof(SetMaxCapacity)}] set-max type={typeof(T).Name} cap={maxCapacity}");

        return true;
    }

    /// <summary>
    /// Gets information about a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to get information about.</typeparam>
    /// <returns>A dictionary containing pool statistics for the type.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetTypeInfo<T>() where T : IPoolable
    {
        System.Type type = typeof(T);
        return _poolDict.TryGetValue(type, out ObjectPool? pool)
            ? pool.GetTypeInfo<T>()
            : new System.Collections.Generic.Dictionary<System.String, System.Object>
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
    /// <returns>The ProtocolType of objects removed.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Int32 ClearPool<T>() where T : IPoolable
    {
        System.Type type = typeof(T);
        return _poolDict.TryGetValue(type, out ObjectPool? pool) ? pool.ClearType<T>() : 0;
    }

    /// <summary>
    /// Clears all objects from all pools.
    /// </summary>
    /// <returns>The total ProtocolType of objects removed.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Int32 ClearAllPools()
    {
        System.Int32 totalRemoved = 0;

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
    /// <returns>The total ProtocolType of objects removed.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Int32 TrimAllPools([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 percentage = 50)
    {
        System.Int32 totalRemoved = 0;

        foreach (var pool in _poolDict.Values)
        {
            totalRemoved += pool.Trim(percentage);
        }

        return totalRemoved;
    }

    /// <summary>
    /// Initialize all statistics for the pool manager.
    /// </summary>
    public void ResetStatistics()
    {
        // Capture snapshot before reset
        System.Int64 gets = System.Threading.Interlocked.Read(ref _totalGetOperations);
        System.Int64 returns = System.Threading.Interlocked.Read(ref _totalReturnOperations);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(ObjectPoolManager)}::{nameof(ResetStatistics)}] " +
                                      $"stats-before-reset gets={gets} returns={returns} " +
                                      $"uptime={Uptime.TotalSeconds:F0}s pools={PoolCount}");

        _ = System.Threading.Interlocked.Exchange(ref _totalGetOperations, 0);
        _ = System.Threading.Interlocked.Exchange(ref _totalReturnOperations, 0);
        _startTime = System.DateTime.UtcNow;

        // Also reset statistics for all pools
        foreach (var pool in _poolDict.Values)
        {
            pool.ResetStatistics();
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[SH.{nameof(ObjectPoolManager)}:{nameof(ResetStatistics)}] stats-reset-complete");
    }

    /// <summary>
    /// Schedules a regular trimming operation to run in the background.
    /// </summary>
    /// <param name="interval">The interval between trim operations.</param>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <param name="cancellationToken">A token to cancel the background trimming.</param>
    /// <returns>A task representing the background trimming operation.</returns>
    public System.Threading.Tasks.Task ScheduleRegularTrimming(
        [System.Diagnostics.CodeAnalysis.NotNull] System.TimeSpan interval,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 percentage = 50,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default)
    {
        return System.Threading.Tasks.Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(interval, cancellationToken);
                    _ = TrimAllPools(percentage);
                }
                catch (System.OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[SH.{nameof(ObjectPoolManager)}:{nameof(ScheduleRegularTrimming)}] trim-task-error", ex);
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Generates a report on the current state of all pools.
    /// </summary>
    /// <returns>A string containing the report.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ObjectPoolManager Status:");
        _ = sb.AppendLine($"Total Pools: {PoolCount}");
        _ = sb.AppendLine($"Total Get Operations: {TotalGetOperations}");
        _ = sb.AppendLine($"Total Return Operations: {TotalReturnOperations}");
        _ = sb.AppendLine($"Uptime: {Uptime.TotalHours:F2} hours");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Pool Details:");
        _ = sb.AppendLine("--------------------------------------------------------------");
        _ = sb.AppendLine("TYPE                     | Available | Max Capacity | Created");
        _ = sb.AppendLine("--------------------------------------------------------------");

        // Sort pools by type name for better readability
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.Type, ObjectPool>> sortedPools = [.. _poolDict];
        sortedPools.Sort((a, b) => System.String.CompareOrdinal(a.Key.Name, b.Key.Name));

        foreach (var kvp in sortedPools)
        {
            System.Type type = kvp.Key;
            ObjectPool pool = kvp.Value;
            var stats = pool.GetStatistics();
            var typeStats = pool.GetTypeInfo<IPoolable>();

            System.String typeName = type.Name.Length > 24
                ? $"{System.MemoryExtensions.AsSpan(type.Name, 0, 21)}..."
                : type.Name.PadRight(24);

            System.Int32 availableCount = System.Convert.ToInt32(typeStats["AvailableCount"]);
            System.Int32 maxCapacity = System.Convert.ToInt32(typeStats["MaxCapacity"]);
            System.Int64 created = System.Convert.ToInt64(stats["TotalCreatedCount"]);

            _ = sb.AppendLine($"{typeName} | {availableCount,9} | {maxCapacity,12} | {created,7}");
        }

        _ = sb.AppendLine("--------------------------------------------------------------");
        return sb.ToString();
    }

    /// <summary>
    /// Gets detailed statistics for all pools.
    /// </summary>
    /// <returns>A dictionary containing statistics for the pool manager and all pools.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetDetailedStatistics()
    {
        var stats = new System.Collections.Generic.Dictionary<System.String, System.Object>
        {
            ["PoolCount"] = PoolCount,
            ["TotalGetOperations"] = TotalGetOperations,
            ["TotalReturnOperations"] = TotalReturnOperations,
            ["UptimeSeconds"] = Uptime.TotalSeconds,
            ["StartTime"] = _startTime,
            ["DefaultMaxPoolSize"] = DefaultMaxPoolSize
        };

        System.Collections.Generic.Dictionary<
            System.String, System.Collections.Generic.Dictionary<
                System.String, System.Object>> poolStats = [];

        foreach (var kvp in _poolDict)
        {
            System.String typeName = kvp.Key.Name;
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private ObjectPool GetOrCreatePool<T>() where T : IPoolable, new()
    {
        System.Type type = typeof(T);
        return _poolDict.GetOrAdd(type, _ => new ObjectPool(_defaultMaxPoolSize));
    }

    #endregion APIs
}