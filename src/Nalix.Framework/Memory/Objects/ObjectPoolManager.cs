// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Concurrency;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Internal.PoolTypes;
using Nalix.Framework.Memory.Pools;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Framework.Memory.Objects;

/// <summary>
/// Provides thread-safe access to a collection of object pools.
/// </summary>
public sealed partial class ObjectPoolManager : IObjectPoolManager, IDisposable
{
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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, byte> _defaultPreallocatedTypes = new();

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

    private readonly int _defaultMaxPoolSize;
    private readonly IRecurringHandle? _trimJob;

    private const int MinimumHealthSample = 32;
    private const double FailureThreshold = 0.1;
    private const double ElevatedFailureThreshold = 0.5;
    private static readonly TimeSpan s_poolFailureLogCooldown = TimeSpan.FromMinutes(15);

    #endregion Fields

    #region Properties

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
        _defaultMaxPoolSize = config.DefaultMaxPoolSize;

        if (_config.EnableObjectTrimming)
        {
            _trimJob = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
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

        bool initialized = false;
        if (pool == null || metrics == null)
        {
            this.InitializePoolAndMetricsFast<T>(id, out pool, out metrics);
            initialized = true;
        }

        if (initialized)
        {
            this.PREALLOC_DEFAULT_FOR_NEW_POOL<T>(pool);
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

        // 1. Lightweight metrics tracking (Safe for Production)
        if (_config.EnableMetrics || _config.EnableDiagnostics)
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
        }

        // 2. Heavyweight diagnostics (Development / QA leaks tracking)
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

        int id = PoolType<T>.Id;
        ObjectPool? pool = id < _pools.Length ? _pools[id] : null;
        PoolMetrics? metrics = id < _metrics.Length ? _metrics[id] : null;

        if (pool == null || metrics == null)
        {
            this.InitializePoolAndMetricsFast<T>(id, out pool, out metrics);
        }

        // Diagnostics Path
        // 1. Heavyweight diagnostics (Development / QA leaks tracking)
        if (_config.EnableDiagnostics)
        {
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
        }

        // 2. Lightweight metrics tracking (Safe for Production)
        if (_config.EnableMetrics || _config.EnableDiagnostics)
        {
            metrics.LastAccessType = "Return";
            metrics.LastAccessUtc = DateTime.UtcNow;

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
        _ = _defaultPreallocatedTypes.TryAdd(type, 0);

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
            _ = Interlocked.Exchange(ref metrics.LastHealthGets, 0);
            _ = Interlocked.Exchange(ref metrics.LastHealthHits, 0);
            _ = Interlocked.Exchange(ref metrics.LastHealthMisses, 0);
            _ = Interlocked.Exchange(ref metrics.LastHealthPeakOutstanding, 0);
            _ = Interlocked.Exchange(ref metrics.LastPoolFailureLogUtcTicks, 0);
            _ = Interlocked.Exchange(ref metrics.LastPoolFailureSeverity, 0);
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
                ["MaxCapacity"] = _defaultMaxPoolSize,
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
            info["Status"] = GET_POOL_STATUS(metrics);

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

        foreach (KeyValuePair<Type, PoolMetrics> kvp in _metricsDict)
        {
            PoolMetrics metrics = kvp.Value;

            long lifetimeGets = Interlocked.Read(ref metrics.TotalGets);
            if (lifetimeGets == 0)
            {
                continue;
            }

            long lifetimeHits = Interlocked.Read(ref metrics.CacheHits);
            long lifetimeMisses = Interlocked.Read(ref metrics.CacheMisses);
            long previousGets = Interlocked.Exchange(ref metrics.LastHealthGets, lifetimeGets);
            _ = Interlocked.Exchange(ref metrics.LastHealthHits, lifetimeHits);
            long previousMisses = Interlocked.Exchange(ref metrics.LastHealthMisses, lifetimeMisses);

            long windowGets = Math.Max(0, lifetimeGets - previousGets);
            long windowMisses = Math.Max(0, lifetimeMisses - previousMisses);

            if (windowGets == 0)
            {
                continue;
            }

            double windowMissRate = windowMisses / (double)windowGets;
            double lifetimeMissRate = lifetimeMisses / (double)lifetimeGets;

            if (windowGets < MinimumHealthSample)
            {
                if (metrics.ConsecutiveFailures >= 2)
                {
                    unhealthyCount++;
                }

                continue;
            }

            if (windowMissRate > FailureThreshold)
            {
                int consecutiveFailures = Interlocked.Increment(ref metrics.ConsecutiveFailures);
                unhealthyCount++;

                this.EMIT_POOL_FAILURE_IF_NEEDED(
                    kvp.Key,
                    metrics,
                    windowGets,
                    windowMisses,
                    windowMissRate,
                    lifetimeGets,
                    lifetimeMissRate,
                    consecutiveFailures);
            }
            else
            {
                _ = Interlocked.Exchange(ref metrics.ConsecutiveFailures, 0);
                _ = Interlocked.Exchange(ref metrics.LastPoolFailureSeverity, 0);
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
    /// Gets or creates an <see cref="ObjectPool"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ObjectPool GetOrCreatePool<T>() where T : IPoolable
    {
        Type type = typeof(T);

        if (_poolDict.TryGetValue(type, out ObjectPool? existing))
        {
            _ = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());
            return existing;
        }

        ObjectPool pool = new(_defaultMaxPoolSize);
        if (_poolDict.TryAdd(type, pool))
        {
            // Update peak pool count on new pool creation.
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

            _ = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());
            return pool;
        }

        _ = _metricsDict.GetOrAdd(type, _ => new PoolMetrics());
        return _poolDict[type];
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GET_POOL_STATUS(PoolMetrics metrics)
    {
        if (Volatile.Read(ref metrics.ConsecutiveFailures) >= 2)
        {
            return "Unhealthy";
        }

        long gets = Interlocked.Read(ref metrics.TotalGets);
        long misses = Interlocked.Read(ref metrics.CacheMisses);
        return gets > 0 && gets < MinimumHealthSample && misses > 0 ? "Warming" : "OK";
    }

    private void EMIT_POOL_FAILURE_IF_NEEDED(
        Type type,
        PoolMetrics metrics,
        long windowGets,
        long windowMisses,
        double windowMissRate,
        long lifetimeGets,
        double lifetimeMissRate,
        int consecutiveFailures)
    {
        int available = 0;
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            Dictionary<string, object> info = pool.GetTypeInfoByType(type);
            if (info.TryGetValue("AvailableCount", out object? value))
            {
                available = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }

        long outstanding = Interlocked.Read(ref metrics.Outstanding);
        long peakOutstanding = Interlocked.Read(ref metrics.PeakOutstanding);
        long previousPeak = Interlocked.Exchange(ref metrics.LastHealthPeakOutstanding, peakOutstanding);
        bool capacityPressure = outstanding > 0 && available == 0 && peakOutstanding > previousPeak;

        int severity = capacityPressure ? 3 : windowMissRate >= ElevatedFailureThreshold ? 2 : 1;
        long nowTicks = DateTime.UtcNow.Ticks;
        long lastLogTicks = Interlocked.Read(ref metrics.LastPoolFailureLogUtcTicks);
        int lastSeverity = Volatile.Read(ref metrics.LastPoolFailureSeverity);
        bool cooldownElapsed = lastLogTicks == 0 ||
            new TimeSpan(nowTicks - lastLogTicks) >= s_poolFailureLogCooldown;

        if (!cooldownElapsed && severity <= lastSeverity)
        {
            return;
        }

        _ = Interlocked.Exchange(ref metrics.LastPoolFailureLogUtcTicks, nowTicks);
        _ = Interlocked.Exchange(ref metrics.LastPoolFailureSeverity, severity);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolFailure, new
            {
                Manager = nameof(ObjectPoolManager),
                Operation = "HealthCheck",
                Type = FORMAT_TYPE_NAME(type),
                WindowGets = windowGets,
                WindowMisses = windowMisses,
                WindowMissRate = windowMissRate,
                LifetimeGets = lifetimeGets,
                LifetimeMissRate = lifetimeMissRate,
                Available = available,
                Outstanding = outstanding,
                PeakOutstanding = peakOutstanding,
                ConsecutiveFailures = consecutiveFailures,
                Reason = capacityPressure ? "CapacityPressure" : "HighWindowMissRate"
            });
        }
    }

    private static string FORMAT_TYPE_NAME(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string name = type.Name;
        int tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            name = name[..tick];
        }

        Type[] args = type.GetGenericArguments();
        string[] argNames = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            argNames[i] = FORMAT_TYPE_NAME(args[i]);
        }

        return string.Concat(name, "<", string.Join(", ", argNames), ">");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PREALLOC_DEFAULT_FOR_NEW_POOL<T>(ObjectPool pool) where T : IPoolable, new()
    {
        int count = _config.DefaultPreallocate;
        if (count <= 0 || !_defaultPreallocatedTypes.TryAdd(typeof(T), 0))
        {
            return;
        }

        int allocated = pool.Prealloc<T>(count);
        if (allocated <= 0)
        {
            return;
        }

        _ = Interlocked.Add(ref _totalCreated, allocated);
        PoolMetrics metrics = _metricsDict.GetOrAdd(typeof(T), _ => new PoolMetrics());
        _ = Interlocked.Add(ref metrics.TotalCreated, allocated);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolExpanded))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolExpanded, new
            {
                Manager = nameof(ObjectPoolManager),
                Operation = "DefaultPreallocate",
                Type = FORMAT_TYPE_NAME(typeof(T)),
                Requested = count,
                Allocated = allocated
            });
        }
    }

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

    #endregion Private Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _trimJob?.Dispose();
    }

    #endregion IDisposable
}
