// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Pools;

namespace Nalix.Framework.Memory.Objects;

/// <summary>
/// Provides thread-safe access to a collection of object pools.
/// </summary>
public sealed class ObjectPoolManager : IReportable
{
    #region Nested Types

    /// <summary>
    /// Detailed metrics for tracking pool performance and health.
    /// </summary>
    private sealed class PoolMetrics
    {
        public long TotalGets;
        public long TotalReturns;

        /// <summary>
        /// Failed to get from pool, created new
        /// </summary>
        public long CacheMisses;

        /// <summary>
        /// Got from pool successfully
        /// </summary>
        public long CacheHits;

        public long TotalCreated;
        public long TotalDisposed;
        public DateTime LastAccessUtc;
        public string? LastAccessType;
        public int ConsecutiveFailures;

        /// <summary>
        /// Number of objects currently checked out (Get without Return)
        /// </summary>
        public long Outstanding;
    }

    #endregion Nested Types

    #region Fields

    /// <summary>
    /// Thread-safe storage for pools
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ObjectPool> _poolDict = new();

    /// <summary>
    /// Per-pool metrics tracking
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PoolMetrics> _metricsDict = new();

    // Configuration

    /// <summary>
    /// Statistics tracking
    /// </summary>
    internal long _totalGetOperations;

    internal long _totalReturnOperations;
    internal long _totalCacheMisses;
    internal long _totalCacheHits;
    internal DateTime _startTime = DateTime.UtcNow;
    internal int _peakPoolCount;

    private long _lastHealthCheckUtc;

    private int _unhealthyPoolCount;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the default maximum size for new pools.
    /// </summary>
    public int DefaultMaxPoolSize
    {
        get;
        set => field = value > 0 ? value : 1024;
    } = 1024;

    /// <summary>
    /// Gets the total number of pools currently managed.
    /// </summary>
    public int PoolCount => _poolDict.Count;

    /// <summary>
    /// Gets the peak number of pools at any time.
    /// </summary>
    public int PeakPoolCount => _peakPoolCount;

    /// <summary>
    /// Gets the total number of get operations performed.
    /// </summary>
    public long TotalGetOperations => Interlocked.Read(ref _totalGetOperations);

    /// <summary>
    /// Gets the total number of return operations performed.
    /// </summary>
    public long TotalReturnOperations => Interlocked.Read(ref _totalReturnOperations);

    /// <summary>
    /// Gets the total number of cache hits (objects retrieved from pool).
    /// </summary>
    public long TotalCacheHits => Interlocked.Read(ref _totalCacheHits);

    /// <summary>
    /// Gets the total number of cache misses (new objects created).
    /// </summary>
    public long TotalCacheMisses => Interlocked.Read(ref _totalCacheMisses);

    /// <summary>
    /// Gets the overall cache hit rate as a percentage (0-100).
    /// </summary>
    public double CacheHitRate
    {
        get
        {
            long total = this.TotalGetOperations;
            return total == 0 ? 0.0 : this.TotalCacheHits / (double)total * 100.0;
        }
    }

    /// <summary>
    /// Gets the uptime of the pool manager.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - _startTime;

    /// <summary>
    /// Gets the number of unhealthy pools (those with high failure rates).
    /// </summary>
    public int UnhealthyPoolCount => Volatile.Read(ref _unhealthyPoolCount);

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPoolManager"/> class.
    /// </summary>
    public ObjectPoolManager() => _lastHealthCheckUtc = DateTime.UtcNow.Ticks;

    #endregion Constructor

    #region APIs

    /// <summary>Gets or creates and returns an instance of <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The poolable type to retrieve.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : IPoolable, new()
    {
        _ = Interlocked.Increment(ref _totalGetOperations);
        ObjectPool pool = this.GetOrCreatePool<T>();

        Type type = typeof(T);
        PoolMetrics metrics = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        // Try to get from pool
        T? result = pool.Get<T>();

        if (!EqualityComparer<T>.Default.Equals(result, default))
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

    /// <summary>Returns an instance of <typeparamref name="T"/> to the pool for future reuse.</summary>
    /// <typeparam name="T">The poolable type to return.</typeparam>
    /// <param name="obj">The object to return.</param>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return<T>(T obj) where T : IPoolable, new()
    {
        if (EqualityComparer<T>.Default.Equals(obj, default))
        {
            throw new ArgumentNullException(nameof(obj), $"Object cannot be null.");
        }

        _ = Interlocked.Increment(ref _totalReturnOperations);
        ObjectPool pool = this.GetOrCreatePool<T>();

        Type type = typeof(T);
        PoolMetrics metrics = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        pool.Return(obj);

        metrics.LastAccessUtc = DateTime.UtcNow;
        metrics.LastAccessType = "Return";
        _ = Interlocked.Increment(ref metrics.TotalReturns);

        // Decrement outstanding; ensure it doesn't go negative
        long outstandingAfter = Interlocked.Decrement(ref metrics.Outstanding);
        if (outstandingAfter < 0)
        {
            // Log and reset to zero to avoid negative counters due to bugs
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SH.{nameof(ObjectPoolManager)}:Return] outstanding-negative type={type.Name} value={outstandingAfter}");

            _ = Interlocked.Exchange(ref metrics.Outstanding, 0);
        }
    }

    /// <summary>Gets or creates a type-specific pool adapter for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    public TypedObjectPoolAdapter<T> GetTypedPool<T>() where T : IPoolable, new()
    {
        ObjectPool pool = this.GetOrCreatePool<T>();
        return new TypedObjectPoolAdapter<T>(pool, this);
    }

    /// <summary>Creates and adds multiple new instances of <typeparamref name="T"/> to the pool.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <param name="count">The number of instances to preallocate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than or equal to zero.</exception>
    public int Prealloc<T>(int count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        ObjectPool pool = this.GetOrCreatePool<T>();
        Type type = typeof(T);
        PoolMetrics metrics = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        int allocated = pool.Prealloc<T>(count);
        _ = Interlocked.Add(ref metrics.TotalCreated, allocated);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[SH.{nameof(ObjectPoolManager)}:{nameof(Prealloc)}] prealloc type={typeof(T).Name} count={count} allocated={allocated}");

        return allocated;
    }

    /// <summary>Sets the maximum capacity for a specific type's pool.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <param name="maxCapacity">The maximum number of items to retain.</param>
    /// <returns><see langword="true"/> when the target pool was updated or created; otherwise, <see langword="false"/>.</returns>
    public bool SetMaxCapacity<T>(int maxCapacity) where T : IPoolable
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
        int currentCount = _poolDict.Count;
        int observed;
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

    /// <summary>Gets information about a specific type's pool.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    public Dictionary<string, object> GetTypeInfo<T>() where T : IPoolable
    {
        Type type = typeof(T);
        Dictionary<string, object> info = _poolDict.TryGetValue(type, out ObjectPool? pool)
            ? pool.GetTypeInfo<T>()
            : new Dictionary<string, object>
            {
                ["TypeName"] = type.Name,
                ["AvailableCount"] = 0,
                ["MaxCapacity"] = this.DefaultMaxPoolSize,
                ["IsActive"] = false
            };

        // Add metrics if available
        if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
        {
            info["CacheHitRate"] = metrics.TotalGets > 0
                ? (metrics.CacheHits / (double)metrics.TotalGets * 100.0)
                : 0.0;
            info["CacheMisses"] = metrics.CacheMisses;
            info["LastAccessUtc"] = metrics.LastAccessUtc;
            info["LastAccessType"] = metrics.LastAccessType ?? "None";
            info["Outstanding"] = metrics.Outstanding;
        }

        return info;
    }

    /// <summary>Clears all objects from a specific type's pool.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    public int ClearPool<T>() where T : IPoolable
    {
        Type type = typeof(T);
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            int removed = pool.ClearType<T>();
            if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
            {
                _ = Interlocked.Add(ref metrics.TotalDisposed, removed);
            }
            return removed;
        }
        return 0;
    }

    /// <summary>Clears all objects from all pools.</summary>
    public int ClearAllPools()
    {
        int totalRemoved = 0;

        foreach (ObjectPool pool in _poolDict.Values)
        {
            totalRemoved += pool.Clear();
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[SH.{nameof(ObjectPoolManager)}:{nameof(ClearAllPools)}] cleared-all total-removed={totalRemoved}");

        return totalRemoved;
    }

    /// <summary>Trims all pools to their target sizes.</summary>
    /// <param name="percentage">The percentage of items to trim from each pool.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by an underlying pool when <paramref name="percentage"/> falls outside the supported trim range.</exception>
    public int TrimAllPools(int percentage = 50)
    {
        int totalRemoved = 0;

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
    public int PerformHealthCheck()
    {
        int unhealthyCount = 0;
        const double FailureThreshold = 0.1; // 10% failure rate

        foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict)
        {
            PoolMetrics metrics = kvp.Value;

            if (metrics.TotalGets == 0)
            {
                continue;
            }

            double missRate = metrics.CacheMisses / (double)metrics.TotalGets;

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
        long gets = Interlocked.Read(ref _totalGetOperations);
        long returns = Interlocked.Read(ref _totalReturnOperations);
        long hits = Interlocked.Read(ref _totalCacheHits);
        long misses = Interlocked.Read(ref _totalCacheMisses);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(ObjectPoolManager)}::{nameof(ResetStatistics)}] " +
                                      $"stats-before-reset gets={gets} returns={returns} hits={hits} misses={misses} " +
                                      $"hit-rate={(gets > 0 ? (hits / (double)gets * 100.0) : 0):F1}% " +
                                      $"uptime={this.Uptime.TotalSeconds:F0}s pools={this.PoolCount}");

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

    /// <summary>Schedules a regular trimming operation to run in the background.</summary>
    /// <param name="interval">The delay between trimming runs.</param>
    /// <param name="percentage">The percentage of items to trim from each pool.</param>
    /// <param name="cancellationToken">The token used to cancel the background loop.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is negative or not supported by <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</exception>
    public Task ScheduleRegularTrimming(
        TimeSpan interval,
        int percentage = 50,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                    _ = this.TrimAllPools(percentage);
                    _ = this.PerformHealthCheck();
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
    public string GenerateReport()
    {
        StringBuilder sb = new(4096);

        // Header
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ObjectPoolManager Status:");
        _ = sb.AppendLine();

        // Overall Statistics
        _ = sb.AppendLine("======================================================================");
        _ = sb.AppendLine("Overall Statistics");
        _ = sb.AppendLine("======================================================================");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Last Heal              : {_lastHealthCheckUtc} Ticks");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Uptime                 : {this.Uptime.TotalHours:F2} hours ({this.Uptime.TotalSeconds:F0}s)");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Pools            : {this.PoolCount} (Peak: {this.PeakPoolCount})");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Unhealthy Pools        : {this.UnhealthyPoolCount}");
        _ = sb.AppendLine();

        // Operation Statistics
        _ = sb.AppendLine("Operation Statistics:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Get Operations   : {this.TotalGetOperations:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Return Operations: {this.TotalReturnOperations:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Net Objects            : {this.TotalGetOperations - this.TotalReturnOperations:N0}");
        _ = sb.AppendLine();

        // Cache Performance
        _ = sb.AppendLine("Cache Performance:");
        long totalOps = this.TotalGetOperations;
        if (totalOps > 0)
        {
            double hitRate = this.TotalCacheHits / (double)totalOps * 100.0;
            double missRate = this.TotalCacheMisses / (double)totalOps * 100.0;
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Cache Hits             : {this.TotalCacheHits:N0} ({hitRate:F2}%)");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Cache Misses           : {this.TotalCacheMisses:N0} ({missRate:F2}%)");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Overall Hit Rate       : {hitRate:F2}%");
        }
        else
        {
            _ = sb.AppendLine("Cache Hits             : 0 (0.00%)");
            _ = sb.AppendLine("Cache Misses           : 0 (0.00%)");
            _ = sb.AppendLine("Overall Hit Rate       : N/A");
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
        sortedPools.Sort((a, b) => string.CompareOrdinal(a.Key.Name, b.Key.Name));

        foreach (KeyValuePair<Type, ObjectPool> kvp in sortedPools)
        {
            Type type = kvp.Key;
            Dictionary<string, object> typeInfo = kvp.Value.GetTypeInfoByType(kvp.Key);

            string typeName = type.Name.Length > 24
                ? $"{type.Name.AsSpan(0, 21)}..."
                : type.Name.PadRight(24);

            int maxCap = Convert.ToInt32(typeInfo["MaxCapacity"], CultureInfo.InvariantCulture);
            int available = Convert.ToInt32(typeInfo["AvailableCount"], CultureInfo.InvariantCulture);

            long gets = 0, hits = 0, misses = 0;
            double hitPercent = 0.0;
            string status = "OK";

            if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
            {
                gets = metrics.TotalGets;
                hits = metrics.CacheHits;
                misses = metrics.CacheMisses;
                hitPercent = gets > 0 ? (hits / (double)gets * 100.0) : 0.0;

                if (metrics.ConsecutiveFailures > 0)
                {
                    status = "⚠ FAIL";
                }
            }

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName} | {available,9} | {maxCap,7} | {gets,7} | {hits,7} | {misses,7} | {hitPercent,6:F1}% | {status}");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Pool Health Details
        if (this.UnhealthyPoolCount > 0)
        {
            _ = sb.AppendLine("Unhealthy Pools:");
            _ = sb.AppendLine("----------------------------------------------------------------------");
            _ = sb.AppendLine("TYPE                     | Consecutive Failures | Last Access");
            _ = sb.AppendLine("----------------------------------------------------------------------");

            foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict.Where(x => x.Value.ConsecutiveFailures > 0))
            {
                string typeName = kvp.Key.Name.Length > 24
                    ? $"{kvp.Key.Name.AsSpan(0, 21)}..."
                    : kvp.Key.Name.PadRight(24);

                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName} | {kvp.Value.ConsecutiveFailures,20} | {kvp.Value.LastAccessUtc:HH:mm:ss}");
            }

            _ = sb.AppendLine("----------------------------------------------------------------------");
            _ = sb.AppendLine();
        }

        // Configuration
        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Default Max s_pool Size  : {this.DefaultMaxPoolSize}");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic report of the object pool manager and all pools.
    /// </summary>
    /// <returns>A dictionary describing the state of the ObjectPoolManager.</returns>
    public IDictionary<string, object> GenerateReportData()
    {
        Dictionary<string, object> data = new(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["UptimeSeconds"] = this.Uptime.TotalSeconds,
            ["PoolCount"] = this.PoolCount,
            ["PeakPoolCount"] = this.PeakPoolCount,
            ["UnhealthyPoolCount"] = this.UnhealthyPoolCount,
            ["DefaultMaxPoolSize"] = this.DefaultMaxPoolSize,
            ["StartTime"] = _startTime,
            ["LastHealthCheckTicks"] = _lastHealthCheckUtc,
            ["TotalGetOperations"] = this.TotalGetOperations,
            ["TotalReturnOperations"] = this.TotalReturnOperations,
            ["TotalCacheHits"] = this.TotalCacheHits,
            ["TotalCacheMisses"] = this.TotalCacheMisses,
            ["CacheHitRate"] = this.CacheHitRate,
            ["NetObjects"] = this.TotalGetOperations - this.TotalReturnOperations,
        };

        List<Dictionary<string, object>> pools = [];

        List<KeyValuePair<Type, ObjectPool>> sortedPools = [.. _poolDict];
        sortedPools.Sort((a, b) => string.CompareOrdinal(a.Key.Name, b.Key.Name));

        foreach (KeyValuePair<Type, ObjectPool> kvp in sortedPools)
        {
            Dictionary<string, object> poolInfo = kvp.Value.GetStatistics();
            Dictionary<string, object> poolDict = new()
            {
                ["Type"] = kvp.Key.FullName ?? kvp.Key.Name,
                ["Available"] = poolInfo.TryGetValue("AvailableCount", out object? available) ? available : 0,
                ["MaxCapacity"] = poolInfo.TryGetValue("MaxCapacity", out object? maxcap) ? maxcap : this.DefaultMaxPoolSize,
                ["IsActive"] = poolInfo.TryGetValue("IsActive", out object? active) ? active : true,
            };

            if (_metricsDict.TryGetValue(kvp.Key, out PoolMetrics? metrics))
            {
                long gets = metrics.TotalGets, hits = metrics.CacheHits, misses = metrics.CacheMisses;
                double hitPercent = gets > 0 ? (hits / (double)gets * 100.0) : 0.0;

                poolDict["Gets"] = gets;
                poolDict["Hits"] = hits;
                poolDict["Misses"] = misses;
                poolDict["HitRate"] = hitPercent;
                poolDict["LastAccessUtc"] = metrics.LastAccessUtc;
                poolDict["LastAccessType"] = metrics.LastAccessType ?? "None";
                poolDict["Outstanding"] = metrics.Outstanding;
                poolDict["ConsecutiveFailures"] = metrics.ConsecutiveFailures;
                poolDict["Status"] = metrics.ConsecutiveFailures > 0 ? "Unhealthy" : "OK";
            }
            pools.Add(poolDict);
        }
        data["Pools"] = pools;

        if (this.UnhealthyPoolCount > 0)
        {
            List<Dictionary<string, object>> unhealthy = [.. _metricsDict
                .Where(x => x.Value.ConsecutiveFailures > 0)
                .Select(x => new Dictionary<string, object>
                {
                    ["Type"] = x.Key.FullName ?? x.Key.Name,
                    ["ConsecutiveFailures"] = x.Value.ConsecutiveFailures,
                    ["LastAccessUtc"] = x.Value.LastAccessUtc,
                    ["Outstanding"] = x.Value.Outstanding
                })];
            data["UnhealthyPools"] = unhealthy;
        }

        return data;
    }

    /// <summary>
    /// Gets or creates an <see cref="ObjectPool"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ObjectPool GetOrCreatePool<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);

        ObjectPool pool = _poolDict.GetOrAdd(type, _ =>
        {
            // Update peak pool count on new pool creation (this is executed while adding)
            int currentCount = _poolDict.Count + 1; // approximate expected count after add
            int observed;
            do
            {
                observed = _peakPoolCount;
                if (currentCount <= observed)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _peakPoolCount, currentCount, observed) != observed);

            return new ObjectPool(this.DefaultMaxPoolSize);
        });

        // Ensure metrics exist for this type
        _ = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        return pool;
    }

    #endregion APIs
}
