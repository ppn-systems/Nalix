// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Environment.Configuration;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Internal.PoolTypes;
using Nalix.Framework.Memory.Pools;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Framework.Memory.Objects;

/// <summary>
/// Provides thread-safe access to a collection of object pools.
/// </summary>
public sealed class ObjectPoolManager : IObjectPoolManager, IReportable
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    private static readonly string RecurringName = "obj.trim";

    /// <summary>
    /// Thread-safe storage for pools
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ObjectPool> _poolDict = new();
    private ObjectPool?[] _pools = new ObjectPool?[64];

    /// <summary>
    /// Per-pool metrics tracking
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PoolMetrics> _metricsDict = new();
    private PoolMetrics?[] _metrics = new PoolMetrics?[64];

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
    private System.Collections.Concurrent.ConcurrentBag<WeakReference<PoolSentinel>> _sentinelTracker = new();

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

    private int _disposed;
    private int _trimCycleCount;
    private long _totalTrimmedObjects;

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
    /// Initializes a new instance of the <see cref="ObjectPoolManager"/> class using the global configuration.
    /// </summary>
    public ObjectPoolManager() : this(ConfigurationManager.Instance.Get<ObjectPoolOptions>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPoolManager"/> class with specific options.
    /// </summary>
    /// <param name="config">The configuration options to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public ObjectPoolManager(ObjectPoolOptions config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));
        _config = config;
        _config.Validate();
        _lastHealthCheckUtc = DateTime.UtcNow.Ticks;

        if (_config.EnableObjectTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()),
                interval: TimeSpan.FromMinutes(Math.Max(1, _config.TrimIntervalMinutes)),
                work: _ =>
                {
                    this.TRIM_EXCESS_OBJECTS();
                    return ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    Jitter = TimeSpan.FromSeconds(5),
                    ExecutionTimeout = TimeSpan.FromSeconds(10),
                    BackoffCap = TimeSpan.FromMinutes(1)
                }
            );
        }
    }

    #endregion Constructor

    #region APIs

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializePoolAndMetricsFast<T>(int id, out ObjectPool pool, out PoolMetrics metrics) where T : IPoolable
    {
        lock (_poolDict)
        {
            if (id >= _pools.Length)
            {
                int newSize = Math.Max(id + 1, _pools.Length * 2);
                Array.Resize(ref _pools, newSize);
                Array.Resize(ref _metrics, newSize);
            }
        }

        pool = this.GetOrCreatePool<T>();
        metrics = _metricsDict.GetOrAdd(typeof(T), _ => new PoolMetrics());

        _pools[id] = pool;
        _metrics[id] = metrics;
    }

    /// <summary>Gets or creates and returns an instance of <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The poolable type to retrieve.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : IPoolable, new()
    {
        _ = Interlocked.Increment(ref _totalGetOperations);

        int id = PoolType<T>.Id;
        ObjectPool? pool = id < _pools.Length ? _pools[id] : null;
        PoolMetrics? metrics = id < _metrics.Length ? _metrics[id] : null;

        if (pool == null || metrics == null)
        {
            this.InitializePoolAndMetricsFast<T>(id, out pool, out metrics);
        }

        (T? result, bool isCacheHit) = pool.GetWithInfoFast<T>(id);

        if (isCacheHit)
        {
            _ = Interlocked.Increment(ref _totalCacheHits);
        }
        else
        {
            _ = Interlocked.Increment(ref _totalCacheMisses);
            _ = Interlocked.Increment(ref _totalCreated);
        }

        if (_config.EnableDiagnostics)
        {
            if (isCacheHit)
            {
                _ = Interlocked.Increment(ref metrics.CacheHits);
            }
            else
            {
                _ = Interlocked.Increment(ref metrics.CacheMisses);
                _ = Interlocked.Increment(ref metrics.TotalCreated);
            }

            _ = Interlocked.Increment(ref metrics.TotalGets);

            // Track outstanding objects so we can detect leaks (Gets - Returns)
            long outstanding = Interlocked.Increment(ref metrics.Outstanding);

            // Update peak outstanding (always tracked for pool metrics)
            if (outstanding > Volatile.Read(ref metrics.PeakOutstanding))
            {
                long currentPeak;
                while (outstanding > (currentPeak = Interlocked.Read(ref metrics.PeakOutstanding)))
                {
                    if (Interlocked.CompareExchange(ref metrics.PeakOutstanding, outstanding, currentPeak) == currentPeak)
                    {
                        break;
                    }
                }
            }

            metrics.LastAccessUtc = DateTime.UtcNow;
            metrics.LastAccessType = "Get";

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

        int id = PoolType<T>.Id;
        ObjectPool? pool = id < _pools.Length ? _pools[id] : null;
        PoolMetrics? metrics = id < _metrics.Length ? _metrics[id] : null;

        if (pool == null || metrics == null)
        {
            this.InitializePoolAndMetricsFast<T>(id, out pool, out metrics);
        }

        // Diagnostics Path
        if (_config.EnableDiagnostics)
        {
            metrics.LastAccessType = "Return";
            metrics.LastAccessUtc = DateTime.UtcNow;

            if (_activeSentinels.TryGetValue(obj, out PoolSentinel? sentinel))
            {
                sentinel.MarkReturned();
                _ = _activeSentinels.Remove(obj);

                long elapsedTicks = Stopwatch.GetTimestamp() - sentinel.RentTimestamp;
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

            _ = Interlocked.Increment(ref metrics.TotalReturns);

            // Decrement outstanding; ensure it doesn't go negative
            long outstandingAfter = Interlocked.Decrement(ref metrics.Outstanding);

            if (outstandingAfter < 0)
            {
                // Log and reset to zero to avoid negative counters due to bugs
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolReturned))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolReturned, new { Manager = nameof(ObjectPoolManager), Operation = "Return", Type = obj.GetType().Name, Outstanding = outstandingAfter, Status = "outstanding-negative" });
                }

                _ = Interlocked.Exchange(ref metrics.Outstanding, 0);
            }
        }

        pool.ReturnFast(obj, id);
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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolExpanded))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolExpanded, new { Manager = nameof(ObjectPoolManager), Operation = nameof(Prealloc), Type = typeof(T).Name, Requested = count, Allocated = allocated });
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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolExpanded))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolExpanded, new { Manager = nameof(ObjectPoolManager), Operation = nameof(SetMaxCapacity), Type = typeof(T).Name, Capacity = maxCapacity });
        }

        return true;
    }

    /// <summary>
    /// Resets all global and per-pool metrics to baseline (zero).
    /// Use this between benchmark runs to ensure a clean slate for diagnostic reports.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                    ? (metrics.TotalLifetimeTicks / (double)metrics.TotalReturns / Stopwatch.Frequency * 1000.0)
                    : 0;
                double maxMs = metrics.MaxLifetimeTicks / (double)Stopwatch.Frequency * 1000.0;

                info["AvgLifetimeMs"] = avgMs;
                info["MaxLifetimeMs"] = maxMs;
                info["p95LifetimeMs"] = this.CALCULATE_P95(metrics);
            }
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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolTrimmed, new { Manager = nameof(ObjectPoolManager), Operation = nameof(ClearAllPools), TotalRemoved = totalRemoved });
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

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolFailure, new { Manager = nameof(ObjectPoolManager), Operation = "HealthCheck", Type = kvp.Key.Name, MissRate = missRate });
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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolReturned))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolReturned, new { Manager = nameof(ObjectPoolManager), Operation = nameof(ResetStatistics), Phase = "BeforeReset", Gets = gets, Returns = returns, Hits = hits, Misses = misses, HitRate = gets > 0 ? (hits / (double)gets * 100.0) : 0.0, UptimeSeconds = this.Uptime.TotalSeconds, this.PoolCount });
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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolReturned))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolReturned, new { Manager = nameof(ObjectPoolManager), Operation = nameof(ResetStatistics), Phase = "ResetComplete" });
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
                double avgMs = metrics.TotalLifetimeTicks / (double)metrics.TotalReturns / Stopwatch.Frequency * 1000.0;
                double maxMs = metrics.MaxLifetimeTicks / (double)Stopwatch.Frequency * 1000.0;
                double p95Ms = this.CALCULATE_P95(metrics);
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

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber("UptimeSeconds", this.Uptime.TotalSeconds);
        writer.WriteNumber(nameof(this.PoolCount), this.PoolCount);
        writer.WriteNumber(nameof(this.PeakPoolCount), this.PeakPoolCount);
        writer.WriteNumber(nameof(this.UnhealthyPoolCount), this.UnhealthyPoolCount);
        writer.WriteNumber(nameof(this.DefaultMaxPoolSize), this.DefaultMaxPoolSize);
        writer.WriteString("StartTime", _startTime);
        writer.WriteNumber("LastHealthCheckTicks", _lastHealthCheckUtc);
        writer.WriteNumber(nameof(this.TotalGetOperations), this.TotalGetOperations);
        writer.WriteNumber(nameof(this.TotalReturnOperations), this.TotalReturnOperations);
        writer.WriteNumber(nameof(this.TotalCacheHits), this.TotalCacheHits);
        writer.WriteNumber(nameof(this.TotalCacheMisses), this.TotalCacheMisses);
        writer.WriteNumber("TotalCreated", Interlocked.Read(ref _totalCreated));
        writer.WriteNumber("TotalDisposed", Interlocked.Read(ref _totalDisposed));
        writer.WriteNumber("TotalLeaked", PoolSentinel.TotalLeaked);
        writer.WriteNumber(nameof(this.CacheHitRate), this.CacheHitRate);
        writer.WriteNumber("Throughput", this.Uptime.TotalSeconds > 0 ? this.TotalGetOperations / this.Uptime.TotalSeconds : 0);
        writer.WriteNumber("CreationRate", this.Uptime.TotalSeconds > 0 ? Interlocked.Read(ref _totalCreated) / this.Uptime.TotalSeconds : 0);
        writer.WriteNumber("NetObjects", this.TotalGetOperations - this.TotalReturnOperations);

        List<KeyValuePair<Type, ObjectPool>> sortedPools = new(_poolDict.Count);
        foreach (KeyValuePair<Type, ObjectPool> kvp in _poolDict)
        {
            sortedPools.Add(kvp);
        }

        sortedPools.Sort((a, b) => string.CompareOrdinal(a.Key.Name, b.Key.Name));

        writer.WriteStartArray("Pools");
        foreach (KeyValuePair<Type, ObjectPool> kvp in sortedPools)
        {
            Dictionary<string, object> poolInfo = kvp.Value.GetTypeInfoByType(kvp.Key);

            writer.WriteStartObject();
            writer.WriteString("Type", kvp.Key.FullName ?? kvp.Key.Name);
            writer.WriteNumber("Available", poolInfo.TryGetValue("AvailableCount", out object? available) ? Convert.ToInt32(available, CultureInfo.InvariantCulture) : 0);
            writer.WriteNumber("MaxCapacity", poolInfo.TryGetValue("MaxCapacity", out object? maxcap) ? Convert.ToInt32(maxcap, CultureInfo.InvariantCulture) : this.DefaultMaxPoolSize);
            writer.WriteBoolean("IsActive", !poolInfo.TryGetValue("IsActive", out object? active) || Convert.ToBoolean(active, CultureInfo.InvariantCulture));

            if (_metricsDict.TryGetValue(kvp.Key, out PoolMetrics? metrics))
            {
                long gets = metrics.TotalGets, hits = metrics.CacheHits, misses = metrics.CacheMisses;
                double hitPercent = gets > 0 ? (hits / (double)gets * 100.0) : 0.0;

                writer.WriteNumber("Gets", gets);
                writer.WriteNumber("Hits", hits);
                writer.WriteNumber("Misses", misses);
                writer.WriteNumber("HitRate", hitPercent);
                writer.WriteString("LastAccessUtc", metrics.LastAccessUtc);
                writer.WriteString("LastAccessType", metrics.LastAccessType ?? "None");
                writer.WriteNumber("Outstanding", metrics.Outstanding);
                writer.WriteNumber("ConsecutiveFailures", metrics.ConsecutiveFailures);
                writer.WriteString("Status", metrics.ConsecutiveFailures > 0 ? "Unhealthy" : "OK");
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        if (this.UnhealthyPoolCount > 0)
        {
            writer.WriteStartArray("UnhealthyPools");
            foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict)
            {
                if (kvp.Value.ConsecutiveFailures <= 0)
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString("Type", kvp.Key.FullName ?? kvp.Key.Name);
                writer.WriteNumber("ConsecutiveFailures", kvp.Value.ConsecutiveFailures);
                writer.WriteString("LastAccessUtc", kvp.Value.LastAccessUtc);
                writer.WriteNumber("Outstanding", kvp.Value.Outstanding);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
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

        long now = Stopwatch.GetTimestamp();
        long thresholdTicks = _config.SuspiciousThresholdSeconds * Stopwatch.Frequency;
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
                    double elapsedSec = elapsed / (double)Stopwatch.Frequency;

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

        if (_sentinelTracker.Count > 10000 && survivors.Count < _sentinelTracker.Count * 0.7)
        {
            ConcurrentBag<WeakReference<PoolSentinel>> newBag = new();
            foreach (WeakReference<PoolSentinel> wr in survivors)
            {
                newBag.Add(wr);
            }

            _sentinelTracker = newBag;
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

    #region Private Methods

    private double CALCULATE_P95(PoolMetrics metrics)
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
        return samples[index] / (double)Stopwatch.Frequency * 1000.0;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TRIM_EXCESS_OBJECTS()
    {
        // Increment trim cycle counter (used for deep trim scheduling)
        int cycle = Interlocked.Increment(ref _trimCycleCount);
        bool isDeepTrim = this.SHOULD_RUN_DEEP_TRIM(cycle);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolTrimmed, new
            {
                Cycle = cycle,
                DeepTrim = isDeepTrim,
                Phase = "ObjectTrimRun"
            });
        }

        int totalRemoved = 0;

        // Take a snapshot to safely iterate while trimming (prevents CollectionModifiedException)
        foreach (KeyValuePair<Type, ObjectPool> kvp in _poolDict.ToArray())
        {
            Type type = kvp.Key;
            if (!_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
            {
                continue;
            }

            int trimPercentage = this.CALCULATE_PER_TYPE_TRIM_PERCENTAGE(type, metrics, isDeepTrim);

            try
            {
                int removed = kvp.Value.Trim(trimPercentage);
                totalRemoved += removed;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // One pool failing must not crash the entire trim job
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolFailure, new
                    {
                        Type = type.Name,
                        Error = ex.Message,
                        Phase = "TrimSinglePool"
                    });
                }
            }
        }

        if (totalRemoved > 0)
        {
            _ = Interlocked.Add(ref _totalTrimmedObjects, totalRemoved);

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
            {
                DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolTrimmed, new
                {
                    Cycle = cycle,
                    DeepTrim = isDeepTrim,
                    TotalRemoved = totalRemoved,
                });
            }
        }

        _ = this.PerformHealthCheck();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SHOULD_RUN_DEEP_TRIM(int cycle)
    {
        // Deep trim runs less frequently than normal trim (e.g. every 6 normal cycles if DeepTrimIntervalMinutes = 30)
        int deepEvery = Math.Max(1, _config.DeepTrimIntervalMinutes / Math.Max(1, _config.TrimIntervalMinutes));
        return cycle % deepEvery == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_PER_TYPE_TRIM_PERCENTAGE(Type type, PoolMetrics metrics, bool isDeepTrim)
    {
        if (isDeepTrim)
        {
            return _config.DeepTrimPercentage; // aggressive trim on deep cycle
        }

        if (metrics.TotalGets == 0)
        {
            // Pool has never been used → trim more aggressively
            return Math.Max(40, _config.BaseKeepPercentage + 15);
        }

        double hitRate = (double)metrics.CacheHits / metrics.TotalGets * 100.0;

        // Get current pool state (available count and capacity)
        int available = 0;
        int maxCap = this.DefaultMaxPoolSize;
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            Dictionary<string, object> info = pool.GetTypeInfoByType(type);
            maxCap = info.TryGetValue("MaxCapacity", out object? mc) ? Convert.ToInt32(mc, CultureInfo.InvariantCulture) : maxCap;
            available = info.TryGetValue("AvailableCount", out object? av) ? Convert.ToInt32(av, CultureInfo.InvariantCulture) : 0;
        }

        double freeRatio = maxCap > 0 ? (double)available / maxCap : 0.0;

        // === SAFETY FLOOR ===
        // Never trim below this threshold to prevent excessive churn and keep recovery fast
        int minKeep = Math.Max(_config.MinimumKeepObjects, maxCap / 12);
        if (available <= minKeep)
        {
            return 0; // already at minimum safe level
        }

        // === HOT POOL (high hit rate) → keep more objects ===
        if (hitRate >= _config.HotHitRateThreshold)
        {
            return 75; // light trim
        }

        // === COLD / UNHEALTHY / IDLE POOL ===
        bool needsAggressive = hitRate < (_config.HotHitRateThreshold - 20.0) || freeRatio > 0.78 || metrics.ConsecutiveFailures > 2;

        if (needsAggressive)
        {
            // Aggressive trim when cache is poor or too many idle objects
            return _config.BaseKeepPercentage + 27;
        }

        // Normal routine trim
        return _config.BaseKeepPercentage;
    }

    #endregion Private Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_config.EnableObjectTrimming)
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelRecurring(TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()));
        }
    }

    #endregion IDisposable
}
