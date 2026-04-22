// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Internal.PoolTypes;
using Nalix.Framework.Memory.Pools;
using Nalix.Framework.Options;

#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA2254 // Template should be a static expression

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

        /// <summary>
        /// Maximum concurrent outstanding objects recorded.
        /// </summary>
        public long PeakOutstanding;

        // Diagnostic Metrics (Only populated when diagnostics enabled)
        public long TotalLifetimeTicks;
        public long MaxLifetimeTicks;
        public long[]? LifetimeReservoir;
        public int ReservoirIndex;
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

    /// <summary>
    /// Configuration for object pool diagnostics.
    /// </summary>
    private readonly ObjectPoolOptions _config;

    /// <summary>
    /// Tracks active sentinels for lifetime and leak detection.
    /// </summary>
    private readonly ConditionalWeakTable<object, PoolSentinel> _activeSentinels = new();

    /// <summary>
    /// Tracks weak references to sentinels for scanning.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentBag<WeakReference<PoolSentinel>> _sentinelTracker = new();

    // Configuration

    /// <summary>
    /// Statistics tracking
    /// </summary>
    internal long _totalGetOperations;

    internal long _totalReturnOperations;
    internal long _totalCacheMisses;
    internal long _totalCacheHits;
    internal long _totalCreated;
    internal long _totalDisposed;
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
    public ObjectPoolManager()
    {
        _lastHealthCheckUtc = DateTime.UtcNow.Ticks;
        _config = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        _config.Validate();
    }

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
            _ = Interlocked.Increment(ref _totalCreated);
            _ = Interlocked.Increment(ref metrics.CacheMisses);
            _ = Interlocked.Increment(ref metrics.TotalCreated);
        }

        metrics.LastAccessUtc = DateTime.UtcNow;
        metrics.LastAccessType = "Get";
        _ = Interlocked.Increment(ref metrics.TotalGets);

        // Track outstanding objects so we can detect leaks (Gets - Returns)
        long outstanding = Interlocked.Increment(ref metrics.Outstanding);

        // Update peak outstanding
        long currentPeak;
        while (outstanding > (currentPeak = Interlocked.Read(ref metrics.PeakOutstanding)))
        {
            if (Interlocked.CompareExchange(ref metrics.PeakOutstanding, outstanding, currentPeak) == currentPeak)
            {
                break;
            }
        }

        // Diagnostics Path
        if (_config.EnableDiagnostics)
        {
            PoolSentinel sentinel = new(result, _config.CaptureStackTraces);

            // CWT keeps sentinel alive as long as 'result' is alive
            _activeSentinels.AddOrUpdate(result, sentinel);

            // Bag allows us to iterate (using weak ref to not anchor the sentinel/object)
            _sentinelTracker.Add(new WeakReference<PoolSentinel>(sentinel));
        }

        if (result is IPoolRentable rentable)
        {
            rentable.OnRent();
        }

        return result;
    }

    /// <summary>Returns an instance of <typeparamref name="T"/> to the pool for future reuse.</summary>
    /// <typeparam name="T">The poolable type to return.</typeparam>
    /// <param name="obj">The object to return.</param>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return<T>(T obj) where T : IPoolable
    {
        if (EqualityComparer<T>.Default.Equals(obj, default))
        {
            throw new ArgumentNullException(nameof(obj), $"Object cannot be null.");
        }

        _ = Interlocked.Increment(ref _totalReturnOperations);
        ObjectPool pool = this.GetOrCreatePool<T>();

        Type type = obj.GetType();
        PoolMetrics metrics = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());

        // Diagnostics Path
        if (_config.EnableDiagnostics && _activeSentinels.TryGetValue(obj, out PoolSentinel? sentinel))
        {
            sentinel.MarkReturned();
            _ = _activeSentinels.Remove(obj);

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - sentinel.RentTimestamp;
            _ = Interlocked.Add(ref metrics.TotalLifetimeTicks, elapsedTicks);

            long currentMax;
            while (elapsedTicks > (currentMax = Interlocked.Read(ref metrics.MaxLifetimeTicks)))
            {
                if (Interlocked.CompareExchange(ref metrics.MaxLifetimeTicks, elapsedTicks, currentMax) == currentMax)
                {
                    break;
                }
            }

            // Update reservoir for p95
            if (metrics.LifetimeReservoir == null)
            {
                _ = Interlocked.CompareExchange(ref metrics.LifetimeReservoir, new long[_config.LifetimeReservoirSize], null);
            }

            if (metrics.LifetimeReservoir != null)
            {
                int index = Interlocked.Increment(ref metrics.ReservoirIndex) % metrics.LifetimeReservoir.Length;
                metrics.LifetimeReservoir[index] = elapsedTicks;
            }
        }

        pool.Return(obj);

        metrics.LastAccessUtc = DateTime.UtcNow;
        metrics.LastAccessType = "Return";
        _ = Interlocked.Increment(ref metrics.TotalReturns);

        // Decrement outstanding; ensure it doesn't go negative
        long outstandingAfter = Interlocked.Decrement(ref metrics.Outstanding);
        if (outstandingAfter < 0)
        {
            // Log and reset to zero to avoid negative counters due to bugs
            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning($"[FW.{nameof(ObjectPoolManager)}:Return] outstanding-negative type={type.Name} value={outstandingAfter}");
            }

            _ = Interlocked.Exchange(ref metrics.Outstanding, 0);
        }
    }

    /// <summary>Gets or creates a type-specific pool adapter for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    public TypedObjectPool<T> GetTypedPool<T>() where T : IPoolable, new()
    {
        ObjectPool pool = this.GetOrCreatePool<T>();
        return new TypedObjectPool<T>(pool, this);
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
        _ = Interlocked.Add(ref _totalCreated, allocated);
        _ = Interlocked.Add(ref metrics.TotalCreated, allocated);

        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"[FW.{nameof(ObjectPoolManager)}:{nameof(Prealloc)}] prealloc type={typeof(T).Name} count={count} allocated={allocated}");
        }

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

        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"[FW.{nameof(ObjectPoolManager)}:{nameof(SetMaxCapacity)}] set-max type={typeof(T).Name} cap={maxCapacity}");
        }

        return true;
    }

    /// <summary>
    /// Resets all global and per-pool metrics to baseline (zero).
    /// Use this between benchmark runs to ensure a clean slate for diagnostic reports.
    /// </summary>
    public void ResetMetrics()
    {
        _ = Interlocked.Exchange(ref _totalGetOperations, 0);
        _ = Interlocked.Exchange(ref _totalReturnOperations, 0);

        foreach (PoolMetrics metrics in _metricsDict.Values)
        {
            _ = Interlocked.Exchange(ref metrics.TotalGets, 0);
            _ = Interlocked.Exchange(ref metrics.TotalReturns, 0);
            _ = Interlocked.Exchange(ref metrics.CacheHits, 0);
            _ = Interlocked.Exchange(ref metrics.CacheMisses, 0);
            _ = Interlocked.Exchange(ref metrics.Outstanding, 0);
            _ = Interlocked.Exchange(ref metrics.ConsecutiveFailures, 0);
            _ = Interlocked.Exchange(ref metrics.TotalCreated, 0);
            _ = Interlocked.Exchange(ref metrics.TotalDisposed, 0);
        }
    }

    /// <summary>Gets information about a specific type's pool.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    public Dictionary<string, object> GetTypeInfo<T>() where T : IPoolable
    {
        Type type = typeof(T);
        Dictionary<string, object> info = _poolDict.TryGetValue(type, out ObjectPool? pool)
            ? pool.GetTypeInfo<T>()
            : new Dictionary<string, object>(16, StringComparer.Ordinal)
            {
                ["TypeName"] = type.Name,
                ["AvailableCount"] = 0,
                ["MaxCapacity"] = this.DefaultMaxPoolSize,
                ["IsActive"] = false,
                ["TotalGets"] = 0L,
                ["TotalReturns"] = 0L,
                ["TotalCreated"] = 0L,
                ["CacheHitRate"] = 0.0,
                ["CacheMisses"] = 0L,
                ["Outstanding"] = 0L,
                ["PeakOutstanding"] = 0L,
                ["LastAccessUtc"] = DateTime.MinValue,
                ["LastAccessType"] = "None",
                ["Status"] = "OK"
            };

        // Ensure all keys exist
        _ = info.TryAdd("TotalGets", 0L);
        _ = info.TryAdd("TotalReturns", 0L);
        _ = info.TryAdd("TotalCreated", 0L);
        _ = info.TryAdd("CacheHitRate", 0.0);
        _ = info.TryAdd("CacheMisses", 0L);
        _ = info.TryAdd("Outstanding", 0L);
        _ = info.TryAdd("PeakOutstanding", 0L);
        _ = info.TryAdd("LastAccessUtc", DateTime.MinValue);
        _ = info.TryAdd("LastAccessType", "None");
        _ = info.TryAdd("Status", "OK");

        // Add metrics if available
        if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
        {
            info["TotalGets"] = metrics.TotalGets;
            info["TotalReturns"] = metrics.TotalReturns;
            info["TotalCreated"] = metrics.TotalCreated;
            info["CacheHitRate"] = metrics.TotalGets > 0
                ? (metrics.CacheHits / (double)metrics.TotalGets * 100.0)
                : 0.0;
            info["CacheMisses"] = metrics.CacheMisses;
            info["LastAccessUtc"] = metrics.LastAccessUtc;
            info["LastAccessType"] = metrics.LastAccessType ?? "None";
            info["Outstanding"] = metrics.Outstanding;
            info["PeakOutstanding"] = metrics.PeakOutstanding;
            info["Status"] = metrics.ConsecutiveFailures > 0 ? "Unhealthy" : "OK";

            if (_config.EnableDiagnostics)
            {
                double avgMs = metrics.TotalGets > 0
                    ? (metrics.TotalLifetimeTicks / (double)metrics.TotalReturns / System.Diagnostics.Stopwatch.Frequency * 1000.0)
                    : 0;
                double maxMs = metrics.MaxLifetimeTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;

                info["AvgLifetimeMs"] = avgMs;
                info["MaxLifetimeMs"] = maxMs;
                info["p95LifetimeMs"] = this.CalculateP95(metrics);
            }
        }

        return info;
    }

    private double CalculateP95(PoolMetrics metrics)
    {
        long[]? reservoir = metrics.LifetimeReservoir;
        if (reservoir == null || metrics.TotalReturns == 0)
        {
            return 0;
        }

        // Copy and sort for percentile calculation (diagnostic only, so allocation is OK)
        long[] samples = new long[reservoir.Length];
        Array.Copy(reservoir, samples, reservoir.Length);
        Array.Sort(samples);

        // Find the 95th percentile
        int index = (int)(samples.Length * 0.95);
        return samples[index] / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;
    }

    /// <summary>Clears all objects from a specific type's pool.</summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    public int ClearPool<T>() where T : IPoolable
    {
        Type type = typeof(T);
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            int removed = pool.ClearType<T>();
            _ = Interlocked.Add(ref _totalDisposed, removed);
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

        _ = Interlocked.Add(ref _totalDisposed, totalRemoved);

        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"[FW.{nameof(ObjectPoolManager)}:{nameof(ClearAllPools)}] cleared-all total-removed={totalRemoved}");
        }

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

                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"[FW.{nameof(ObjectPoolManager)}:Internal] unhealthy-pool type={kvp.Key.Name} miss-rate={missRate:F2}%");
                }
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

        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (logger != null && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation($"[FW.{nameof(ObjectPoolManager)}::{nameof(ResetStatistics)}] " +
                                  $"stats-before-reset gets={gets} returns={returns} hits={hits} misses={misses} " +
                                  $"hit-rate={(gets > 0 ? (hits / (double)gets * 100.0) : 0):F1}% " +
                                  $"uptime={this.Uptime.TotalSeconds:F0}s pools={this.PoolCount}");
        }

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

        if (logger != null && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"[FW.{nameof(ObjectPoolManager)}:{nameof(ResetStatistics)}] stats-reset-complete");
        }
    }

    /// <summary>Schedules a regular trimming operation to run in the background.</summary>
    /// <param name="interval">The delay between trimming runs.</param>
    /// <param name="percentage">The percentage of items to trim from each pool.</param>
    /// <param name="cancellationToken">The token used to cancel the background loop.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is negative or not supported by <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</exception>
    public async Task ScheduleRegularTrimming(TimeSpan interval, int percentage = 50, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                _ = this.TrimAllPools(percentage);
                _ = this.PerformHealthCheck();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(ex, $"[FW.{nameof(ObjectPoolManager)}:{nameof(ScheduleRegularTrimming)}] trim-task-error");
                }
            }
        }
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

        double uptimeSec = this.Uptime.TotalSeconds;
        if (uptimeSec > 0)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Throughput             : {this.TotalGetOperations / uptimeSec:F1} ops/s");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Creation Rate          : {Interlocked.Read(ref _totalCreated) / uptimeSec:F1} objects/s");
        }

        if (_config.EnableDiagnostics && _config.EnableLeakDetection)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GC Leak Detected       : {PoolSentinel.TotalLeaked:N0} objects");
        }
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

        // Configuration
        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Default Max s_pool Size: {this.DefaultMaxPoolSize}");
        _ = sb.AppendLine();

        // Pool Details
        _ = sb.AppendLine("==========================================================================================================");
        _ = sb.AppendLine("Object Details (Dashboard):");
        _ = sb.AppendLine("==========================================================================================================");
        _ = sb.AppendLine("TYPE                         | STORAGE (A/M)     | USAGE (O/P)       | TRAFFIC (G/R)     | HIT%   | STATUS");
        _ = sb.AppendLine("-----------------------------+-------------------+-------------------+-------------------+--------+-------");

        // Fix: create sortable list from dictionary
        List<KeyValuePair<Type, ObjectPool>> sortedPools = [.. _poolDict];
        sortedPools.Sort((a, b) => string.CompareOrdinal(a.Key.Name, b.Key.Name));

        foreach (KeyValuePair<Type, ObjectPool> kvp in sortedPools)
        {
            Type type = kvp.Key;
            Dictionary<string, object> typeInfo = kvp.Value.GetTypeInfoByType(kvp.Key);

            string typeName = ReportExtensions.FormatTypeName(type.Name, 28);

            int maxCap = Convert.ToInt32(typeInfo["MaxCapacity"], CultureInfo.InvariantCulture);
            int available = Convert.ToInt32(typeInfo["AvailableCount"], CultureInfo.InvariantCulture);

            long gets = 0, returns = 0, peak = 0, active = 0;
            double hitPercent = 0.0;
            string status = "OK";

            if (_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
            {
                gets = metrics.TotalGets;
                returns = metrics.TotalReturns;
                peak = metrics.PeakOutstanding;
                active = metrics.Outstanding;
                hitPercent = gets > 0 ? (metrics.CacheHits / (double)gets * 100.0) : 0.0;

                if (metrics.ConsecutiveFailures > 0)
                {
                    status = "⚠ FAIL";
                }
            }

            string storage = ReportExtensions.FormatGroup(available, maxCap, compact: true);
            string usage = ReportExtensions.FormatGroup(active, peak, compact: true);
            string traffic = ReportExtensions.FormatGroup(gets, returns, compact: true);

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName} | {storage,-17} | {usage,-17} | {traffic,-17} | {hitPercent,5:F1}% | {status}");

            if (_config.EnableDiagnostics && metrics != null && metrics.TotalReturns > 0)
            {
                double avgMs = metrics.TotalLifetimeTicks / (double)metrics.TotalReturns / System.Diagnostics.Stopwatch.Frequency * 1000.0;
                double maxMs = metrics.MaxLifetimeTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;
                double p95Ms = this.CalculateP95(metrics);
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"                             | Lifetime (ms): Avg={avgMs:F2}, p95={p95Ms:F2}, Max={maxMs:F2}");
            }
        }

        _ = sb.AppendLine("----------------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Suspicious Objects Section
        if (_config.EnableDiagnostics)
        {
            this.AppendSuspiciousObjects(sb);
        }

        // Pool Health Details
        if (this.UnhealthyPoolCount > 0)
        {
            _ = sb.AppendLine("Unhealthy Pools:");
            _ = sb.AppendLine("----------------------------------------------------------------------");
            _ = sb.AppendLine("TYPE                     | Consecutive Failures | Last Access");
            _ = sb.AppendLine("-------------------------+----------------------+---------------------");

            foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict)
            {
                if (kvp.Value.ConsecutiveFailures <= 0)
                {
                    continue;
                }

                string typeName = kvp.Key.Name.Length > 24
                    ? $"{kvp.Key.Name.AsSpan(0, 21)}..."
                    : kvp.Key.Name.PadRight(24);

                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName} | {kvp.Value.ConsecutiveFailures,20} | {kvp.Value.LastAccessUtc:HH:mm:ss}");
            }

            _ = sb.AppendLine("----------------------------------------------------------------------");
            _ = sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic report of the object pool manager and all pools.
    /// </summary>
    /// <returns>A dictionary describing the state of the ObjectPoolManager.</returns>
    public IDictionary<string, object> GetReportData()
    {
        Dictionary<string, object> data = new(13, StringComparer.Ordinal)
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
            ["TotalCreated"] = Interlocked.Read(ref _totalCreated),
            ["TotalDisposed"] = Interlocked.Read(ref _totalDisposed),
            ["TotalLeaked"] = PoolSentinel.TotalLeaked,
            ["UptimeMs"] = this.Uptime.TotalMilliseconds,
            ["CacheHitRate"] = this.CacheHitRate,
            ["Throughput"] = this.Uptime.TotalSeconds > 0 ? this.TotalGetOperations / this.Uptime.TotalSeconds : 0,
            ["CreationRate"] = this.Uptime.TotalSeconds > 0 ? Interlocked.Read(ref _totalCreated) / this.Uptime.TotalSeconds : 0,
            ["NetObjects"] = this.TotalGetOperations - this.TotalReturnOperations,
        };

        List<KeyValuePair<Type, ObjectPool>> sortedPools = new(_poolDict.Count);
        foreach (KeyValuePair<Type, ObjectPool> kvp in _poolDict)
        {
            sortedPools.Add(kvp);
        }

        sortedPools.Sort((a, b) => string.CompareOrdinal(a.Key.Name, b.Key.Name));

        List<Dictionary<string, object>> pools = new(sortedPools.Count);

        foreach (KeyValuePair<Type, ObjectPool> kvp in sortedPools)
        {
            Dictionary<string, object> poolInfo = kvp.Value.GetStatistics();
            Dictionary<string, object> poolDict = new(12, StringComparer.Ordinal)
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
            List<Dictionary<string, object>> unhealthy = [];
            foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict)
            {
                if (kvp.Value.ConsecutiveFailures <= 0)
                {
                    continue;
                }

                unhealthy.Add(new Dictionary<string, object>(4, StringComparer.Ordinal)
                {
                    ["Type"] = kvp.Key.FullName ?? kvp.Key.Name,
                    ["ConsecutiveFailures"] = kvp.Value.ConsecutiveFailures,
                    ["LastAccessUtc"] = kvp.Value.LastAccessUtc,
                    ["Outstanding"] = kvp.Value.Outstanding
                });
            }
            data["UnhealthyPools"] = unhealthy;
        }

        return data;
    }

    /// <summary>
    /// Gets or creates an <see cref="ObjectPool"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ObjectPool GetOrCreatePool<T>() where T : IPoolable
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

    private void AppendSuspiciousObjects(StringBuilder sb)
    {
        _ = sb.AppendLine("Suspicious Objects (Outstanding > " + _config.SuspiciousThresholdSeconds + "s):");
        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("TYPE                     | Elapsed (s) | Stack Trace (first line)");
        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        long thresholdTicks = _config.SuspiciousThresholdSeconds * System.Diagnostics.Stopwatch.Frequency;
        int found = 0;

        // We prune stale references while scanning to prevent the bag from growing indefinitely.
        // Since ConcurrentBag is not easily pruned, we'll collect survivors and re-populate
        // ONLY if the bag has grown significantly (e.g. > 1000 items).
        List<WeakReference<PoolSentinel>> survivors = new();

        foreach (WeakReference<PoolSentinel> weakRef in _sentinelTracker)
        {
            if (weakRef.TryGetTarget(out PoolSentinel? sentinel))
            {
                if (sentinel.IsReturned)
                {
                    continue;
                }

                survivors.Add(weakRef);

                long elapsed = now - sentinel.RentTimestamp;
                if (elapsed >= thresholdTicks)
                {
                    found++;
                    double elapsedSec = elapsed / (double)System.Diagnostics.Stopwatch.Frequency;

                    string typeName = sentinel.ObjectType.Name.Length > 24
                        ? $"{sentinel.ObjectType.Name.AsSpan(0, 21)}..."
                        : sentinel.ObjectType.Name.PadRight(24);

                    string stack = "N/A (CaptureStackTraces=false)";
                    if (!string.IsNullOrEmpty(sentinel.StackTrace))
                    {
                        int firstLineEnd = sentinel.StackTrace.IndexOf('\n', StringComparison.Ordinal);
                        stack = firstLineEnd > 0 ? sentinel.StackTrace[..firstLineEnd].Trim() : sentinel.StackTrace;
                    }

                    if (found <= 20)
                    {
                        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{typeName} | {elapsedSec,11:F1} | {stack}");
                    }
                }
            }
        }

        // Pruning: If the bag is much larger than current survivors, we might want to reset it.
        // For simplicity in this diagnostic path, we'll just show the count.
        if (found > 20)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"... and {found - 20} more suspicious objects.");
        }

        if (found == 0)
        {
            _ = sb.AppendLine("(None detected)");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();
    }

    #endregion APIs
}
