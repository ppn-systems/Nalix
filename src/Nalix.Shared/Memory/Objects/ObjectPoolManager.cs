// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nalix.Common.Diagnostics;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Shared.Memory.Objects;

/// <summary>
/// Provides thread-safe access to a collection of object pools containing instances of <see cref="IPoolable"/>.
/// Tracks comprehensive metrics including hit/miss rates, allocation patterns, and pool health.
/// </summary>
public sealed class ObjectPoolManager : IReportable
{
    #region Nested Types

    /// <summary>
    /// Detailed metrics for tracking pool performance and health.
    /// </summary>
    private sealed class PoolMetrics
    {
        public Int64 TotalGets;
        public Int64 TotalReturns;
        public Int64 CacheMisses;       // Failed to get from pool, created new
        public Int64 CacheHits;         // Got from pool successfully
        public Int64 TotalCreated;
        public Int64 TotalDisposed;
        public DateTime LastAccessUtc;
        public String? LastAccessType;
        public Int32 ConsecutiveFailures;

        // Number of objects currently checked out (Get without Return)
        public Int64 Outstanding;
    }

    #endregion Nested Types

    #region Fields

    // Thread-safe storage for pools
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ObjectPool> _poolDict = new();

    // Per-pool metrics tracking
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PoolMetrics> _metricsDict = new();

    // Configuration

    // Statistics tracking
    internal Int64 _totalGetOperations;
    internal Int64 _totalReturnOperations;
    internal Int64 _totalCacheMisses;
    internal Int64 _totalCacheHits;
    internal DateTime _startTime = DateTime.UtcNow;
    internal Int32 _peakPoolCount;

    // Health monitoring
    private Int64 _lastHealthCheckUtc;
    private Int32 _unhealthyPoolCount;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the default maximum size for new pools.
    /// </summary>
    public Int32 DefaultMaxPoolSize
    {
        get;
        set => field = value > 0 ? value : 1024;
    } = 1024;

    /// <summary>
    /// Gets the total number of pools currently managed.
    /// </summary>
    public Int32 PoolCount => _poolDict.Count;

    /// <summary>
    /// Gets the peak number of pools at any time.
    /// </summary>
    public Int32 PeakPoolCount => _peakPoolCount;

    /// <summary>
    /// Gets the total number of get operations performed.
    /// </summary>
    public Int64 TotalGetOperations => Interlocked.Read(ref _totalGetOperations);

    /// <summary>
    /// Gets the total number of return operations performed.
    /// </summary>
    public Int64 TotalReturnOperations => Interlocked.Read(ref _totalReturnOperations);

    /// <summary>
    /// Gets the total number of cache hits (objects retrieved from pool).
    /// </summary>
    public Int64 TotalCacheHits => Interlocked.Read(ref _totalCacheHits);

    /// <summary>
    /// Gets the total number of cache misses (new objects created).
    /// </summary>
    public Int64 TotalCacheMisses => Interlocked.Read(ref _totalCacheMisses);

    /// <summary>
    /// Gets the overall cache hit rate as a percentage (0-100).
    /// </summary>
    public Double CacheHitRate
    {
        get
        {
            Int64 total = TotalGetOperations;
            return total == 0 ? 0.0 : TotalCacheHits / (Double)total * 100.0;
        }
    }

    /// <summary>
    /// Gets the uptime of the pool manager.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - _startTime;

    /// <summary>
    /// Gets the number of unhealthy pools (those with high failure rates).
    /// </summary>
    public Int32 UnhealthyPoolCount => Volatile.Read(ref _unhealthyPoolCount);

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPoolManager"/> class.
    /// </summary>
    public ObjectPoolManager() => _lastHealthCheckUtc = DateTime.UtcNow.Ticks;

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Gets or creates and returns an instance of <typeparamref name="T"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public T Get<T>() where T : IPoolable, new()
    {
        _ = Interlocked.Increment(ref _totalGetOperations);
        ObjectPool pool = GetOrCreatePool<T>();

        Type type = typeof(T);
        PoolMetrics metrics = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        // Try to get from pool
        T? result = pool.Get<T>();

        if (result != null)
        {
            // Hit from pool
            _ = Interlocked.Increment(ref _totalCacheHits);
            _ = Interlocked.Increment(ref metrics.CacheHits);
        }
        else
        {
            // Miss: create new instance rather than calling pool.Get again
            result = new T();
            _ = Interlocked.Increment(ref _totalCacheMisses);
            _ = Interlocked.Increment(ref metrics.CacheMisses);
            _ = Interlocked.Increment(ref metrics.TotalCreated);
        }

        metrics.LastAccessUtc = DateTime.UtcNow;
        metrics.LastAccessType = "Get";
        _ = Interlocked.Increment(ref metrics.TotalGets);

        // Track outstanding objects so we can detect leaks (Gets - Returns)
        _ = Interlocked.Increment(ref metrics.Outstanding);

        return result;
    }

    /// <summary>
    /// Returns an instance of <typeparamref name="T"/> to the pool for future reuse.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return<T>([System.Diagnostics.CodeAnalysis.NotNull] T obj) where T : IPoolable, new()
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        _ = Interlocked.Increment(ref _totalReturnOperations);
        ObjectPool pool = GetOrCreatePool<T>();

        Type type = typeof(T);
        PoolMetrics metrics = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        pool.Return(obj);

        metrics.LastAccessUtc = DateTime.UtcNow;
        metrics.LastAccessType = "Return";
        _ = Interlocked.Increment(ref metrics.TotalReturns);

        // Decrement outstanding; ensure it doesn't go negative
        Int64 outstandingAfter = Interlocked.Decrement(ref metrics.Outstanding);
        if (outstandingAfter < 0)
        {
            // Log and reset to zero to avoid negative counters due to bugs
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Warn($"[SH.{nameof(ObjectPoolManager)}:Return] outstanding-negative type={type.Name} value={outstandingAfter}");
            _ = Interlocked.Exchange(ref metrics.Outstanding, 0);
        }
    }

    /// <summary>
    /// Gets or creates a type-specific pool adapter for more efficient operations with a specific type.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public TypedObjectPoolAdapter<T> GetTypedPool<T>() where T : IPoolable, new()
    {
        ObjectPool pool = GetOrCreatePool<T>();
        return new TypedObjectPoolAdapter<T>(pool, this);
    }

    /// <summary>
    /// Creates and adds multiple new instances of <typeparamref name="T"/> to the pool.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Int32 Prealloc<T>([System.Diagnostics.CodeAnalysis.NotNull] Int32 count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        ObjectPool pool = GetOrCreatePool<T>();
        Type type = typeof(T);
        PoolMetrics metrics = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        Int32 allocated = pool.Prealloc<T>(count);
        _ = Interlocked.Add(ref metrics.TotalCreated, allocated);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[SH.{nameof(ObjectPoolManager)}:{nameof(Prealloc)}] prealloc type={typeof(T).Name} count={count} allocated={allocated}");

        return allocated;
    }

    /// <summary>
    /// Sets the maximum capacity for a specific type's pool.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Boolean SetMaxCapacity<T>([System.Diagnostics.CodeAnalysis.NotNull] Int32 maxCapacity) where T : IPoolable
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

        pool = new ObjectPool(maxCapacity);
        _poolDict[type] = pool;

        // Update peak pool count (use Interlocked to avoid races)
        Int32 currentCount = _poolDict.Count;
        Int32 observed;
        do
        {
            observed = _peakPoolCount;
            if (currentCount <= observed)
            {
                break;
            }
        } while (Interlocked.CompareExchange(ref _peakPoolCount, currentCount, observed) != observed);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                        .Debug($"[SH.{nameof(ObjectPoolManager)}:{nameof(SetMaxCapacity)}] set-max type={typeof(T).Name} cap={maxCapacity}");

        return true;
    }

    /// <summary>
    /// Gets information about a specific type's pool.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Dictionary<String, Object> GetTypeInfo<T>() where T : IPoolable
    {
        Type type = typeof(T);
        Dictionary<String, Object> info = _poolDict.TryGetValue(type, out ObjectPool? pool)
            ? pool.GetTypeInfo<T>()
            : new Dictionary<String, Object>
            {
                ["TypeName"] = type.Name,
                ["AvailableCount"] = 0,
                ["MaxCapacity"] = DefaultMaxPoolSize,
                ["IsActive"] = false
            };

        // Add metrics if available
        if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
        {
            info["CacheHitRate"] = metrics.TotalGets > 0
                ? (metrics.CacheHits / (Double)metrics.TotalGets * 100.0)
                : 0.0;
            info["CacheMisses"] = metrics.CacheMisses;
            info["LastAccessUtc"] = metrics.LastAccessUtc;
            info["LastAccessType"] = metrics.LastAccessType ?? "None";
            info["Outstanding"] = metrics.Outstanding;
        }

        return info;
    }

    /// <summary>
    /// Clears all objects from a specific type's pool.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Int32 ClearPool<T>() where T : IPoolable
    {
        Type type = typeof(T);
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            Int32 removed = pool.ClearType<T>();
            if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
            {
                _ = Interlocked.Add(ref metrics.TotalDisposed, removed);
            }
            return removed;
        }
        return 0;
    }

    /// <summary>
    /// Clears all objects from all pools.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Int32 ClearAllPools()
    {
        Int32 totalRemoved = 0;

        foreach (ObjectPool pool in _poolDict.Values)
        {
            totalRemoved += pool.Clear();
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[SH.{nameof(ObjectPoolManager)}:{nameof(ClearAllPools)}] cleared-all total-removed={totalRemoved}");

        return totalRemoved;
    }

    /// <summary>
    /// Trims all pools to their target sizes.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Int32 TrimAllPools([System.Diagnostics.CodeAnalysis.NotNull] Int32 percentage = 50)
    {
        Int32 totalRemoved = 0;

        foreach (ObjectPool pool in _poolDict.Values)
        {
            totalRemoved += pool.Trim(percentage);
        }

        return totalRemoved;
    }

    /// <summary>
    /// Performs a health check on all pools and identifies unhealthy ones.
    /// </summary>
    /// <returns>Number of unhealthy pools detected.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Int32 PerformHealthCheck()
    {
        Int32 unhealthyCount = 0;
        const Double FailureThreshold = 0.1; // 10% failure rate

        foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict)
        {
            PoolMetrics metrics = kvp.Value;

            if (metrics.TotalGets == 0)
            {
                continue;
            }

            Double missRate = metrics.CacheMisses / (Double)metrics.TotalGets;

            if (missRate > FailureThreshold)
            {
                unhealthyCount++;
                _ = Interlocked.Increment(ref metrics.ConsecutiveFailures);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[SH.{nameof(ObjectPoolManager)}:Internal] unhealthy-pool type={kvp.Key.Name} miss-rate={missRate:F2}%");
            }
            else
            {
                metrics.ConsecutiveFailures = 0;
            }
        }

        Volatile.Write(ref _unhealthyPoolCount, unhealthyCount);
        _lastHealthCheckUtc = DateTime.UtcNow.Ticks;

        return unhealthyCount;
    }

    /// <summary>
    /// Initialize all statistics for the pool manager.
    /// </summary>
    public void ResetStatistics()
    {
        // Capture snapshot before reset
        Int64 gets = Interlocked.Read(ref _totalGetOperations);
        Int64 returns = Interlocked.Read(ref _totalReturnOperations);
        Int64 hits = Interlocked.Read(ref _totalCacheHits);
        Int64 misses = Interlocked.Read(ref _totalCacheMisses);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(ObjectPoolManager)}::{nameof(ResetStatistics)}] " +
                                      $"stats-before-reset gets={gets} returns={returns} hits={hits} misses={misses} " +
                                      $"hit-rate={(gets > 0 ? (hits / (Double)gets * 100.0) : 0):F1}% " +
                                      $"uptime={Uptime.TotalSeconds:F0}s pools={PoolCount}");

        _ = Interlocked.Exchange(ref _totalGetOperations, 0);
        _ = Interlocked.Exchange(ref _totalReturnOperations, 0);
        _ = Interlocked.Exchange(ref _totalCacheHits, 0);
        _ = Interlocked.Exchange(ref _totalCacheMisses, 0);
        _startTime = DateTime.UtcNow;

        // Also reset statistics for all pools
        foreach (ObjectPool pool in _poolDict.Values)
        {
            pool.ResetStatistics();
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(ObjectPoolManager)}:{nameof(ResetStatistics)}] stats-reset-complete");
    }

    /// <summary>
    /// Schedules a regular trimming operation to run in the background.
    /// </summary>
    public System.Threading.Tasks.Task ScheduleRegularTrimming(
        [System.Diagnostics.CodeAnalysis.NotNull] TimeSpan interval,
        [System.Diagnostics.CodeAnalysis.NotNull] Int32 percentage = 50,
        [System.Diagnostics.CodeAnalysis.NotNull] CancellationToken cancellationToken = default)
    {
        return System.Threading.Tasks.Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(interval, cancellationToken);
                    _ = TrimAllPools(percentage);
                    _ = PerformHealthCheck();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[SH.{nameof(ObjectPoolManager)}:{nameof(ScheduleRegularTrimming)}] trim-task-error", ex);
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Generates a comprehensive report on the current state of all pools with detailed metrics.
    /// </summary>
    /// <returns>A string containing the detailed report.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public String GenerateReport()
    {
        System.Text.StringBuilder sb = new(4096);

        // Header
        _ = sb.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ObjectPoolManager Status:");
        _ = sb.AppendLine();

        // Overall Statistics
        _ = sb.AppendLine("======================================================================");
        _ = sb.AppendLine("Overall Statistics");
        _ = sb.AppendLine("======================================================================");
        _ = sb.AppendLine($"Uptime                 : {Uptime.TotalHours:F2} hours ({Uptime.TotalSeconds:F0}s)");
        _ = sb.AppendLine($"Total Pools            : {PoolCount} (Peak: {PeakPoolCount})");
        _ = sb.AppendLine($"Unhealthy Pools        : {UnhealthyPoolCount}");
        _ = sb.AppendLine();

        // Operation Statistics
        _ = sb.AppendLine("Operation Statistics:");
        _ = sb.AppendLine($"Total Get Operations   : {TotalGetOperations:N0}");
        _ = sb.AppendLine($"Total Return Operations: {TotalReturnOperations:N0}");
        _ = sb.AppendLine($"Net Objects            : {TotalGetOperations - TotalReturnOperations:N0}");
        _ = sb.AppendLine();

        // Cache Performance
        _ = sb.AppendLine("Cache Performance:");
        Int64 totalOps = TotalGetOperations;
        if (totalOps > 0)
        {
            Double hitRate = TotalCacheHits / (Double)totalOps * 100.0;
            Double missRate = TotalCacheMisses / (Double)totalOps * 100.0;
            _ = sb.AppendLine($"Cache Hits             : {TotalCacheHits:N0} ({hitRate:F2}%)");
            _ = sb.AppendLine($"Cache Misses           : {TotalCacheMisses:N0} ({missRate:F2}%)");
            _ = sb.AppendLine($"Overall Hit Rate       : {hitRate:F2}%");
        }
        else
        {
            _ = sb.AppendLine($"Cache Hits             : 0 (0.00%)");
            _ = sb.AppendLine($"Cache Misses           : 0 (0.00%)");
            _ = sb.AppendLine($"Overall Hit Rate       : N/A");
        }
        _ = sb.AppendLine();

        // Pool Details
        _ = sb.AppendLine("==============================================================================================");
        _ = sb.AppendLine("s_pool Details:");
        _ = sb.AppendLine("==============================================================================================");
        _ = sb.AppendLine("TYPE                     | Available | Max Cap | Gets    | Hits    | Misses  | Hit%    | Status");
        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");

        // Fix: create sortable list from dictionary
        List<KeyValuePair<Type, ObjectPool>> sortedPools = [.. _poolDict];
        sortedPools.Sort((a, b) => System.String.CompareOrdinal(a.Key.Name, b.Key.Name));

        foreach (KeyValuePair<Type, ObjectPool> kvp in sortedPools)
        {
            Type type = kvp.Key;
            Dictionary<String, Object> typeInfo = kvp.Value.GetTypeInfoByType(kvp.Key);

            String typeName = type.Name.Length > 24
                ? $"{MemoryExtensions.AsSpan(type.Name, 0, 21)}..."
                : type.Name.PadRight(24);

            Int32 available = Convert.ToInt32(typeInfo["AvailableCount"]);
            Int32 maxCap = Convert.ToInt32(typeInfo["MaxCapacity"]);

            Int64 gets = 0, hits = 0, misses = 0;
            Double hitPercent = 0.0;
            String status = "OK";

            if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
            {
                gets = metrics.TotalGets;
                hits = metrics.CacheHits;
                misses = metrics.CacheMisses;
                hitPercent = gets > 0 ? (hits / (Double)gets * 100.0) : 0.0;

                if (metrics.ConsecutiveFailures > 0)
                {
                    status = "⚠ FAIL";
                }
            }

            _ = sb.AppendLine($"{typeName} | {available,9} | {maxCap,7} | {gets,7} | {hits,7} | {misses,7} | {hitPercent,6:F1}% | {status}");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Pool Health Details
        if (UnhealthyPoolCount > 0)
        {
            _ = sb.AppendLine("Unhealthy Pools:");
            _ = sb.AppendLine("----------------------------------------------------------------------");
            _ = sb.AppendLine("TYPE                     | Consecutive Failures | Last Access");
            _ = sb.AppendLine("----------------------------------------------------------------------");

            foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict.Where(x => x.Value.ConsecutiveFailures > 0))
            {
                String typeName = kvp.Key.Name.Length > 24
                    ? $"{MemoryExtensions.AsSpan(kvp.Key.Name, 0, 21)}..."
                    : kvp.Key.Name.PadRight(24);

                _ = sb.AppendLine($"{typeName} | {kvp.Value.ConsecutiveFailures,20} | {kvp.Value.LastAccessUtc:HH:mm:ss}");
            }

            _ = sb.AppendLine("----------------------------------------------------------------------");
            _ = sb.AppendLine();
        }

        // Configuration
        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine($"Default Max s_pool Size  : {DefaultMaxPoolSize}");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Gets detailed statistics for all pools including cache performance metrics.
    /// </summary>
    /// <returns>A dictionary containing comprehensive statistics.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public Dictionary<String, Object> GetDetailedStatistics()
    {
        Dictionary<string, object> stats = new Dictionary<String, Object>
        {
            ["PoolCount"] = PoolCount,
            ["PeakPoolCount"] = PeakPoolCount,
            ["TotalGetOperations"] = TotalGetOperations,
            ["TotalReturnOperations"] = TotalReturnOperations,
            ["TotalCacheHits"] = TotalCacheHits,
            ["TotalCacheMisses"] = TotalCacheMisses,
            ["CacheHitRate"] = CacheHitRate,
            ["UnhealthyPoolCount"] = UnhealthyPoolCount,
            ["UptimeSeconds"] = Uptime.TotalSeconds,
            ["StartTime"] = _startTime,
            ["DefaultMaxPoolSize"] = DefaultMaxPoolSize
        };

        Dictionary<string, Dictionary<string, object>> poolStats = new Dictionary<String, Dictionary<String, Object>>();

        foreach (KeyValuePair<Type, ObjectPool> kvp in _poolDict)
        {
            String typeName = kvp.Key.Name;
            Dictionary<String, Object> baseStats = kvp.Value.GetStatistics();

            // Add metrics if available
            if (_metricsDict.TryGetValue(kvp.Key, out PoolMetrics? metrics))
            {
                baseStats["CacheHits"] = metrics.CacheHits;
                baseStats["CacheMisses"] = metrics.CacheMisses;
                baseStats["CacheHitRate"] = metrics.TotalGets > 0
                    ? (metrics.CacheHits / (Double)metrics.TotalGets * 100.0)
                    : 0.0;
                baseStats["LastAccessUtc"] = metrics.LastAccessUtc;
                baseStats["LastAccessType"] = metrics.LastAccessType ?? "None";
                baseStats["ConsecutiveFailures"] = metrics.ConsecutiveFailures;
                baseStats["Outstanding"] = metrics.Outstanding;
            }

            poolStats[typeName] = baseStats;
        }

        stats["Pools"] = poolStats;

        return stats;
    }

    /// <summary>
    /// Gets or creates an <see cref="ObjectPool"/> for <typeparamref name="T"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private ObjectPool GetOrCreatePool<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);

        ObjectPool pool = _poolDict.GetOrAdd(type, _ =>
        {
            // Update peak pool count on new pool creation (this is executed while adding)
            Int32 currentCount = _poolDict.Count + 1; // approximate expected count after add
            Int32 observed;
            do
            {
                observed = _peakPoolCount;
                if (currentCount <= observed)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _peakPoolCount, currentCount, observed) != observed);

            return new ObjectPool(DefaultMaxPoolSize);
        });

        // Ensure metrics exist for this type
        _ = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        return pool;
    }

    #endregion APIs
}
