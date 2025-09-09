// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Memory.Pooling;

/// <summary>
/// Manages buffers of various sizes with optimized allocation/deallocation and optional trimming.
/// </summary>
public sealed class BufferPoolManager : System.IDisposable, IReportable
{
    #region Fields & Constants

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.String, (System.Int32, System.Double)[]> _allocationPatternCache;

    private readonly System.Int32 _totalBuffers;
    private readonly System.Boolean _enableTrimming;
    private readonly System.Boolean _enableAnalytics;
    private readonly System.Boolean _enableQueueCompaction;
    private readonly System.Boolean _secureClear;
    private readonly System.Boolean _fallbackToArrayPool;
    private readonly System.Int32 _autoTuneOpThreshold;
    private readonly System.Double _adaptiveGrowthFactor;
    private readonly System.Double _maxMemoryPct;
    private readonly System.Int32 _minIncrease;
    private readonly System.Int32 _maxIncrease;
    private readonly System.Int32 _trimIntervalMinutes;
    private readonly System.Int32 _deepTrimIntervalMinutes;

    private readonly (System.Int32 BufferSize, System.Double Allocation)[] _bufferAllocations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, System.Int32> _suitablePoolSizeCache;

    private readonly System.Buffers.ArrayPool<System.Byte> _fallbackArrayPool = System.Buffers.ArrayPool<System.Byte>.Shared;
    private readonly BufferPoolCollection _poolManager;

    private System.Threading.Timer? _trimTimer;
    private System.Int32 _trimCycleCount;
    private System.Boolean _isInitialized;

    #endregion Fields & Constants

    #region Properties

    /// <summary>Gets the largest buffer size from the buffer allocations list.</summary>
    public System.Int32 MaxBufferSize { get; }

    /// <summary>Gets the smallest buffer size from the buffer allocations list.</summary>
    public System.Int32 MinBufferSize { get; }

    /// <summary>
    /// Gets the recurring name used for buffer trimming operations.
    /// </summary>
    public static readonly System.String RecurringName;

    #endregion Properties

    #region Constructors

    static BufferPoolManager()
    {
        _allocationPatternCache = new();
        RecurringName = $"buf.trim.{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(typeof(BufferPoolManager)):X8}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with validation and trimming.
    /// </summary>
    public BufferPoolManager() : this(bufferConfig: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with validation and trimming.
    /// </summary>
    public BufferPoolManager(BufferConfig? bufferConfig = null)
    {
        BufferConfig config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferConfig>();

        _suitablePoolSizeCache = new();

        _totalBuffers = config.TotalBuffers;
        _enableTrimming = config.EnableMemoryTrimming;
        _enableAnalytics = config.EnableAnalytics;
        _enableQueueCompaction = config.EnableQueueCompaction;
        _secureClear = config.SecureClear;
        _fallbackToArrayPool = config.FallbackToArrayPool;
        _autoTuneOpThreshold = config.AutoTuneOperationThreshold;
        _adaptiveGrowthFactor = config.AdaptiveGrowthFactor;
        _maxMemoryPct = config.MaxMemoryPercentage;
        _minIncrease = config.MinimumIncrease;
        _maxIncrease = config.MaxBufferIncreaseLimit;
        _trimIntervalMinutes = config.TrimIntervalMinutes;
        _deepTrimIntervalMinutes = config.DeepTrimIntervalMinutes;

        _bufferAllocations = ParseBufferAllocations(config.BufferAllocations);

        MinBufferSize = System.Linq.Enumerable.Min(_bufferAllocations, alloc => alloc.BufferSize);
        MaxBufferSize = System.Linq.Enumerable.Max(_bufferAllocations, alloc => alloc.BufferSize);

        _poolManager = new BufferPoolCollection(secureClear: _secureClear, autoTuneThreshold: _autoTuneOpThreshold);
        _poolManager.EventShrink += ShrinkBufferPoolSize;
        _poolManager.EventIncrease += IncreaseBufferPoolSize;

        this.AllocateBuffers();

        if (_enableTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: RecurringName,
                interval: System.TimeSpan.FromMinutes(System.Math.Max(1, _trimIntervalMinutes)),
                work: ct =>
                {
                    TrimExcessBuffers(null);
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    Tag = "bufpool",
                    NonReentrant = true,                           // không chồng lần chạy
                    Jitter = System.TimeSpan.FromSeconds(5),       // lệch nhịp nhẹ tránh đồng pha
                    RunTimeout = System.TimeSpan.FromSeconds(3),   // bảo hiểm nếu trim lâu
                    MaxBackoff = System.TimeSpan.FromMinutes(1)    // backoff khi lỗi liên tiếp
                }
            );
        }
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Rents a buffer of at least the requested size with optimized caching and optional fallback.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] Rent(System.Int32 size = 256)
    {
        // Fast path for exact matches to common sizes
        if (size is 256 or 512 or 1024 or 2048 or 4096)
        {
            return _poolManager.RentBuffer(size);
        }

        if (_suitablePoolSizeCache.TryGetValue(size, out System.Int32 cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            System.Byte[] buffer = _poolManager.RentBuffer(size);

            if (_enableAnalytics)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(BufferPoolManager)}] rent-fast size={size}");
            }

            if (size > 64 && size < 1_000_000 && _suitablePoolSizeCache.Count < 1000)
            {
                _ = _suitablePoolSizeCache.TryAdd(size, buffer.Length);
            }

            return buffer;
        }
        catch (System.ArgumentException ex)
        {
            // Size exceeds the largest pool; optionally fall back to ArrayPool to avoid crashing.
            if (_fallbackToArrayPool)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Warn($"[{nameof(BufferPoolManager)}] fallback size={size} msg={ex.Message}");

                return _fallbackArrayPool.Rent(size);
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[{nameof(BufferPoolManager)}] rent-fail size={size} msg={ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Returns the buffer to the appropriate pool with safety checks and fallback.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return(System.Byte[]? buffer)
    {
        if (buffer is null)
        {
            return;
        }

        try
        {
            _poolManager.ReturnBuffer(buffer);

            if (_enableAnalytics)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(BufferPoolManager)}] return size={buffer.Length}");
            }
        }
        catch (System.ArgumentException ex)
        {
            // Buffer length does not map to any known pool (likely from fallback ArrayPool).
            if (_fallbackToArrayPool)
            {
                // Clear on sensitive workloads if configured.
                if (_secureClear)
                {
                    System.Array.Clear(buffer, 0, buffer.Length);
                }

                _fallbackArrayPool.Return(buffer, clearArray: false);
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(BufferPoolManager)}] return-fallback size={buffer.Length}");
                return;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(BufferPoolManager)}] return-fail size={buffer.Length} msg={ex.Message}");
        }
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    public System.Double GetAllocationForSize(System.Int32 size)
    {
        if (size > MaxBufferSize)
        {
            return System.Linq.Enumerable.Last(_bufferAllocations).Allocation;
        }

        if (size <= MinBufferSize)
        {
            return System.Linq.Enumerable.First(_bufferAllocations).Allocation;
        }

        System.Int32 left = 0;
        System.Int32 right = _bufferAllocations.Length - 1;

        while (left <= right)
        {
            System.Int32 mid = left + ((right - left) / 2);
            System.Int32 midSize = _bufferAllocations[mid].BufferSize;

            if (midSize == size)
            {
                return _bufferAllocations[mid].Allocation;
            }

            if (midSize < size)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return left < _bufferAllocations.Length ? _bufferAllocations[left].Allocation
                                                : System.Linq.Enumerable.Last(_bufferAllocations).Allocation;
    }

    /// <summary>
    /// Generates a report on the current state of the buffer pools.
    /// </summary>
    /// <returns>A string containing the report.</returns>
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] BufferPoolManager Status:");
        _ = sb.AppendLine($"Initialized: {_isInitialized}");
        _ = sb.AppendLine($"Total Buffers (Configured): {_totalBuffers}");
        _ = sb.AppendLine($"Pools: {_bufferAllocations.Length}");
        _ = sb.AppendLine($"Min Buffer Size: {MinBufferSize}");
        _ = sb.AppendLine($"Max Buffer Size: {MaxBufferSize}");
        _ = sb.AppendLine($"Enable Trimming: {_enableTrimming}");
        _ = sb.AppendLine($"Enable Analytics: {_enableAnalytics}");
        _ = sb.AppendLine($"Enable SecureClear: {_secureClear}");
        _ = sb.AppendLine($"Fallback to ArrayPool: {_fallbackToArrayPool}");
        _ = sb.AppendLine($"Trim Interval (min): {_trimIntervalMinutes}");
        _ = sb.AppendLine($"Deep Trim Interval (min): {_deepTrimIntervalMinutes}");
        _ = sb.AppendLine($"Trim Cycles Run: {_trimCycleCount}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Pool Details:");
        _ = sb.AppendLine("--------------------------------------------------------------------");
        _ = sb.AppendLine("Size     | Total Buffers | Free Buffers | In Use | Usage % | MissRate");
        _ = sb.AppendLine("--------------------------------------------------------------------");

        foreach (var pool in System.Linq.Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            System.Int32 inUse = info.TotalBuffers - info.FreeBuffers;
            System.Double usage = info.GetUsageRatio() * 100.0;
            System.Double miss = info.GetMissRate() * 100.0;

            _ = sb.AppendLine($"{info.BufferSize,8} | {info.TotalBuffers,13} | {info.FreeBuffers,12} | {inUse,6} | {usage,7:F2}% | {miss,7:F2}%");
        }

        _ = sb.AppendLine("--------------------------------------------------------------------");

        return sb.ToString();
    }


    #endregion Public API

    #region Private: Allocation & Trimming

    /// <summary>
    /// Allocates buffers based on the configuration settings.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AllocateBuffers()
    {
        if (_isInitialized)
        {
            return;
        }

        foreach (var (bufferSize, allocation) in _bufferAllocations)
        {
            System.Int32 capacity = System.Math.Max(1, (System.Int32)(_totalBuffers * allocation));
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(BufferPoolManager)}] " +
                                      $"init-ok total={_totalBuffers} pools={_bufferAllocations.Length} min={MinBufferSize} max={MaxBufferSize}");
    }

    /// <summary>
    /// Periodically trims excess buffers to reduce memory footprint.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void TrimExcessBuffers(System.Object? _)
    {
        System.Int32 cycle = System.Threading.Interlocked.Increment(ref _trimCycleCount);
        System.Int32 deepEvery = System.Math.Max(1, _deepTrimIntervalMinutes / System.Math.Max(1, _trimIntervalMinutes));
        System.Boolean deepTrim = (cycle % deepEvery) == 0;

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[{nameof(BufferPoolManager)}] trim-run deep={deepTrim}");

        // 1) Memory budget check
        System.Int64 totalAvailable = System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        System.Int64 targetBudget = (System.Int64)(totalAvailable * _maxMemoryPct);

        System.Int64 currentBudget = 0;
        foreach (var pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
            currentBudget += info.TotalBuffers * (System.Int64)info.BufferSize;
        }

        // 2) Shrink aggressively if above budget
        System.Boolean overBudget = currentBudget > targetBudget;

        foreach (var pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            // Basic heuristic: prefer shrinking pools with high free ratio or low usage.
            System.Double usage = info.GetUsageRatio();
            System.Boolean candidateByFree = info.FreeBuffers >= (System.Int32)(info.TotalBuffers * 0.50); // aligns with CanShrink
            System.Boolean candidateByOverBudget = overBudget || deepTrim;

            if (candidateByFree || candidateByOverBudget)
            {
                // Use a capped shrink step to avoid oscillation.
                System.Int32 shrinkStep = System.Math.Min(_maxIncrease, System.Math.Max(1, info.FreeBuffers - (System.Int32)(info.TotalBuffers * 0.5)));
                if (shrinkStep > 0)
                {
                    pool.DecreaseCapacity(shrinkStep);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Meta($"[{nameof(BufferPoolManager)}] " +
                                                  $"trim-shrink size={info.BufferSize} step={shrinkStep} usage={usage:F2}");
                }
            }
        }
    }

    #endregion Private: Allocation & Trimming

    #region Private: Resize Strategies

    /// <summary>
    /// Shrinks the buffer pool size using an optimized algorithm.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ShrinkBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        System.Double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        System.Int32 targetBuffers = (System.Int32)(targetAllocation * _totalBuffers);

        System.Int32 minimumBuffers = System.Math.Max(1, poolInfo.TotalBuffers >> 2);
        System.Int32 excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        System.Int32 safetyMargin = (System.Int32)System.Math.Min(20, System.Math.Sqrt(minimumBuffers));

        System.Int32 buffersToShrink = System.Math.Clamp(excessBuffers - safetyMargin, 0, _maxIncrease);

        if (buffersToShrink > 0)
        {
            System.Boolean lockTaken = false;
            System.Threading.SpinLock spinLock = new(false);

            try
            {
                spinLock.Enter(ref lockTaken);

                if (pool.FreeBuffers > targetBuffers + safetyMargin)
                {
                    pool.DecreaseCapacity(buffersToShrink);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(BufferPoolManager)}] shrink size={poolInfo.BufferSize} by={buffersToShrink}");
                }
            }
            finally
            {
                if (lockTaken)
                {
                    spinLock.Exit();
                }
            }
        }
    }

    /// <summary>
    /// Increases the buffer pool size using an optimized algorithm.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void IncreaseBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        System.Int32 threshold = System.Math.Max(1, (System.Int32)(poolInfo.TotalBuffers * 0.25));

        if (poolInfo.FreeBuffers <= threshold)
        {
            System.Int32 baseIncreasePow2 = System.Math.Max(_minIncrease,
                (System.Int32)System.Numerics.BitOperations.RoundUpToPowerOf2(
                    (System.UInt32)System.Math.Max(1, poolInfo.TotalBuffers >> 2)));

            System.Double missRatio = poolInfo.GetMissRate();
            System.Int32 scaled = (System.Int32)System.Math.Ceiling(baseIncreasePow2 * (missRatio > 0.5 ? 2.0 : 1.0) * _adaptiveGrowthFactor);

            System.Int32 maxIncrease = System.Math.Min(scaled, _maxIncrease);

            System.Boolean lockTaken = false;
            System.Threading.SpinLock spinLock = new(false);

            try
            {
                spinLock.Enter(ref lockTaken);

                if (pool.FreeBuffers <= threshold)
                {
                    pool.IncreaseCapacity(maxIncrease);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(BufferPoolManager)}] " +
                                                   $"increase size={poolInfo.BufferSize} by={maxIncrease} miss={missRatio:F2}");
                }
            }
            finally
            {
                if (lockTaken)
                {
                    spinLock.Exit();
                }
            }
        }
    }

    #endregion Private: Resize Strategies

    #region Parsing

    /// <summary>
    /// Parses the buffer allocation settings with caching for repeated configurations.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static (System.Int32, System.Double)[] ParseBufferAllocations(System.String bufferAllocationsString)
    {
        return System.String.IsNullOrWhiteSpace(bufferAllocationsString)
            ? throw new System.ArgumentException($"[{nameof(BufferPoolManager)}] The input string must not be blank.", nameof(bufferAllocationsString))
            : _allocationPatternCache.GetOrAdd(bufferAllocationsString, key =>
        {
            try
            {
                var allocations = ParseAllocations(key, bufferAllocationsString);

                System.Double totalAllocation = System.Linq.Enumerable.Sum(allocations, a => a.ratio);
                return totalAllocation > 1.1
                    ? throw new System.ArgumentException($"[{nameof(BufferPoolManager)}] Total allocation ratio ({totalAllocation:F2}) exceeds 1.0.")
                    : ((System.Int32, System.Double)[])allocations;
            }
            catch (System.Exception ex) when (ex is System.FormatException or System.ArgumentException or System.OverflowException or System.ArgumentOutOfRangeException)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(BufferPoolManager)}] " +
                                               $"alloc-parse-fail str='{bufferAllocationsString}' msg={ex.Message}");

                throw new System.ArgumentException(
                    $"[{nameof(BufferPoolManager)}] Malformed allocation string. Expected '<size>,<ratio>;...'. ERROR: {ex.Message}");
            }
        });
    }

    private static (System.Int32 allocationSize, System.Double ratio)[] ParseAllocations(System.String key, System.String bufferAllocationsString)
    {
        // Split by ';'
        System.String[] pairs = key.Split(';', System.StringSplitOptions.RemoveEmptyEntries);

        var list = new System.Collections.Generic.List<(System.Int32, System.Double)>();

        foreach (System.String pair in pairs)
        {
            System.String[] parts = pair.Split(',', System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new System.FormatException(
                    $"[{nameof(BufferPoolManager)}] Incorrectly formatted pair: '{pair}'.");
            }

            // Parse size
            if (!System.Int32.TryParse(parts[0].Trim(), out System.Int32 allocationSize) || allocationSize <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(bufferAllocationsString),
                    $"[{nameof(BufferPoolManager)}] Size must be > 0.");
            }

            // Parse ratio
            if (!System.Double.TryParse(parts[1].Trim(), out System.Double ratio) || ratio <= 0 || ratio > 1)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(bufferAllocationsString),
                    $"[{nameof(BufferPoolManager)}] Ratio must be (0,1].");
            }

            list.Add((allocationSize, ratio));
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));

        return [.. list];
    }

    #endregion Parsing

    #region IDisposable

    /// <summary>
    /// Releases all resources of the buffer pools.
    /// </summary>
    public void Dispose()
    {
        _trimTimer?.Dispose();
        _trimTimer = null;

        _suitablePoolSizeCache.Clear();
        _poolManager.Dispose();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(BufferPoolManager)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}