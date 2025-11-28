// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Memory.Pooling;

/// <summary>
/// Manages buffers of various sizes with optimized allocation/deallocation and optional trimming.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public sealed class BufferPoolManager : System.IDisposable, IReportable
{
    #region Fields & Constants

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.String, (System.Int32, System.Double)[]> _allocationPatternCache;

    private readonly System.Int64 _maxMemoryBytes;
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
        _maxMemoryBytes = config.MaxMemoryBytes;

        _bufferAllocations = ParseBufferAllocations(config.BufferAllocations);

        MinBufferSize = System.Linq.Enumerable.Min(_bufferAllocations, alloc => alloc.BufferSize);
        MaxBufferSize = System.Linq.Enumerable.Max(_bufferAllocations, alloc => alloc.BufferSize);

        _poolManager = new BufferPoolCollection(bufferConfig: config);
        _poolManager.EventShrink += ShrinkBufferPoolSize;
        _poolManager.EventIncrease += IncreaseBufferPoolSize;

        AllocateBuffers();

        if (_enableTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: RecurringName,
                interval: System.TimeSpan.FromMinutes(System.Math.Max(1, _trimIntervalMinutes)),
                work: _ =>
                {
                    TrimExcessBuffers(null);
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    Tag = "bufpool",
                    NonReentrant = true,
                    Jitter = System.TimeSpan.FromSeconds(5),
                    ExecutionTimeout = System.TimeSpan.FromSeconds(5),
                    BackoffCap = System.TimeSpan.FromMinutes(1)
                }
            );
        }
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Rents a buffer of at least the requested size with optimized caching and optional fallback.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Byte[] Rent([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 size = 256)
    {
        if (IsFastCommonSize(size))
        {
            return _poolManager.RentBuffer(size);
        }

        if (_suitablePoolSizeCache.TryGetValue(size, out System.Int32 cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            return RentFromPoolsWithCaching(size);
        }
        catch (System.ArgumentException ex)
        {
            return HandleRentFailure(size, ex);
        }
    }

    /// <summary>
    /// Returns the buffer to the appropriate pool with safety checks and fallback.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return([System.Diagnostics.CodeAnalysis.MaybeNull] System.Byte[]? buffer)
    {
        if (buffer is null)
        {
            return;
        }

        try
        {
            ReturnToManagedPools(buffer);
        }
        catch (System.ArgumentException ex)
        {
            HandleReturnFailure(buffer, ex);
        }
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Double GetAllocationForSize([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 size)
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
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();

        AppendReportHeader(sb);
        AppendReportPoolDetails(sb);

        return sb.ToString();
    }

    #endregion Public API

    #region Private: Rent / Return helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsFastCommonSize(System.Int32 size) => size is 256 or 512 or 1024 or 2048 or 4096;

    /// <summary>
    /// Rents buffer from configured pools and optionally updates size cache.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Byte[] RentFromPoolsWithCaching(System.Int32 size)
    {
        System.Byte[] buffer = _poolManager.RentBuffer(size);

        if (_enableAnalytics)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(BufferPoolManager)}] rent-fast size={size}");
        }

        CacheSuitablePoolSize(size, buffer.Length);

        return buffer;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CacheSuitablePoolSize(System.Int32 requestedSize, System.Int32 actualSize)
    {
        if (requestedSize <= 64 || requestedSize >= 1_000_000)
        {
            return;
        }

        if (_suitablePoolSizeCache.Count >= 1000)
        {
            return;
        }

        _ = _suitablePoolSizeCache.TryAdd(requestedSize, actualSize);
    }

    /// <summary>
    /// Handles rent failure by optionally falling back to ArrayPool.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Byte[] HandleRentFailure(System.Int32 size, System.ArgumentException ex)
    {
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (_fallbackToArrayPool)
        {
            logger?.Warn($"[{nameof(BufferPoolManager)}] fallback size={size} msg={ex.Message}");
            return _fallbackArrayPool.Rent(size);
        }

        logger?.Error($"[{nameof(BufferPoolManager)}] rent-fail size={size} msg={ex.Message}", ex);
        throw ex;
    }

    /// <summary>
    /// Returns a buffer to managed Nalix pools and emits analytics if enabled.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ReturnToManagedPools(System.Byte[] buffer)
    {
        _poolManager.ReturnBuffer(buffer);

        if (_enableAnalytics)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(BufferPoolManager)}] return size={buffer.Length}");
        }
    }

    /// <summary>
    /// Handles return failure by optionally returning buffer to fallback ArrayPool.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void HandleReturnFailure(System.Byte[] buffer, System.ArgumentException ex)
    {
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (_fallbackToArrayPool)
        {
            if (_secureClear)
            {
                System.Array.Clear(buffer, 0, buffer.Length);
            }

            _fallbackArrayPool.Return(buffer, clearArray: false);
            logger?.Debug($"[{nameof(BufferPoolManager)}] return-fallback size={buffer.Length}");
            return;
        }

        logger?.Warn($"[{nameof(BufferPoolManager)}] return-fail size={buffer.Length} msg={ex.Message}");
    }

    #endregion Private: Rent / Return helpers

    #region Private: Allocation & Trimming

    /// <summary>
    /// Allocates buffers based on the configuration settings.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void TrimExcessBuffers(System.Object? _)
    {
        System.Int32 cycle = System.Threading.Interlocked.Increment(ref _trimCycleCount);
        System.Boolean deepTrim = ShouldRunDeepTrim(cycle);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[{nameof(BufferPoolManager)}] trim-run deep={deepTrim}");

        (System.Int64 targetBudget, System.Int64 currentUsage, System.Boolean overBudget) = ComputeMemoryBudget();

        // avoid warning unused (có thể log thêm nếu muốn)
        _ = targetBudget;
        _ = currentUsage;

        foreach (var pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            if (!ShouldTrimPool(info, overBudget, deepTrim))
            {
                continue;
            }

            System.Int32 shrinkStep = CalculateTrimShrinkStep(in info);
            if (shrinkStep <= 0)
            {
                continue;
            }

            TrimSinglePool(pool, in info, shrinkStep);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean ShouldRunDeepTrim(System.Int32 cycle)
    {
        System.Int32 deepEvery = System.Math.Max(1, _deepTrimIntervalMinutes / System.Math.Max(1, _trimIntervalMinutes));
        return (cycle % deepEvery) == 0;
    }

    /// <summary>
    /// Computes memory budget and current usage for the buffer pools.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private (System.Int64 TargetBudget, System.Int64 CurrentUsage, System.Boolean OverBudget) ComputeMemoryBudget()
    {
        System.Int64 totalAvailable = System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        System.Int64 percentBudget = (System.Int64)(totalAvailable * _maxMemoryPct);
        System.Int64 hardCap = _maxMemoryBytes > 0 ? _maxMemoryBytes : System.Int64.MaxValue;
        System.Int64 targetBudget = System.Math.Min(percentBudget, hardCap);

        System.Int64 current = 0;
        foreach (var pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
            current += info.TotalBuffers * (System.Int64)info.BufferSize;
        }

        System.Boolean overBudget = current > targetBudget;
        return (targetBudget, current, overBudget);
    }

    /// <summary>
    /// Determines whether a pool is a candidate for trimming.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean ShouldTrimPool(
        in BufferPoolState info,
        System.Boolean overBudget,
        System.Boolean deepTrim)
    {
        System.Double usage = info.GetUsageRatio();
        System.Boolean candidateByFree = info.FreeBuffers >= (System.Int32)(info.TotalBuffers * 0.50);
        System.Boolean candidateByOverBudget = overBudget || deepTrim;

        // không sử dụng usage trực tiếp ở đây nhưng giữ lại để dễ tùy chỉnh sau này
        _ = usage;

        return candidateByFree || candidateByOverBudget;
    }

    /// <summary>
    /// Calculates shrink step for trimming pass.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CalculateTrimShrinkStep(in BufferPoolState info)
    {
        System.Int32 desiredFree = (System.Int32)(info.TotalBuffers * 0.5);
        System.Int32 excess = info.FreeBuffers - desiredFree;
        if (excess <= 0)
        {
            return 0;
        }

        return System.Math.Min(_maxIncrease, System.Math.Max(1, excess));
    }

    /// <summary>
    /// Applies trim on a single pool and logs the operation.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void TrimSinglePool(BufferPoolShared pool, in BufferPoolState info, System.Int32 shrinkStep)
    {
        System.Double usage = info.GetUsageRatio();

        pool.DecreaseCapacity(shrinkStep);
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[{nameof(BufferPoolManager)}] " +
                                      $"trim-shrink size={info.BufferSize} step={shrinkStep} usage={usage:F2}");
    }

    #endregion Private: Allocation & Trimming

    #region Private: Resize Strategies

    /// <summary>
    /// Shrinks the buffer pool size using an optimized algorithm.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ShrinkBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        System.Int32 buffersToShrink = CalculateShrinkAmount(in poolInfo);
        if (buffersToShrink <= 0)
        {
            return;
        }

        if (!ShouldApplyShrink(pool, in poolInfo, buffersToShrink))
        {
            return;
        }

        pool.DecreaseCapacity(buffersToShrink);

        ref readonly BufferPoolState latest = ref pool.GetPoolInfoRef();
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[{nameof(BufferPoolManager)}] shrink size={latest.BufferSize} by={buffersToShrink}");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CalculateShrinkAmount(in BufferPoolState poolInfo)
    {
        System.Double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        System.Int32 targetBuffers = (System.Int32)(targetAllocation * _totalBuffers);

        System.Int32 minimumBuffers = System.Math.Max(1, poolInfo.TotalBuffers >> 2);
        System.Int32 excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        System.Int32 safetyMargin = (System.Int32)System.Math.Min(20, System.Math.Sqrt(minimumBuffers));

        return System.Math.Clamp(excessBuffers - safetyMargin, 0, _maxIncrease);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean ShouldApplyShrink(
        BufferPoolShared pool,
        in BufferPoolState poolInfo,
        System.Int32 buffersToShrink)
    {
        if (buffersToShrink <= 0)
        {
            return false;
        }

        // double-check với trạng thái mới nhất trước khi shrink
        System.Int32 currentFree = pool.FreeBuffers;
        System.Double allocation = (poolInfo.TotalBuffers <= 0)
            ? 0.0
            : (System.Double)currentFree / poolInfo.TotalBuffers;

        _ = allocation; // reserved cho tuning sau này

        System.Int32 targetFree = poolInfo.FreeBuffers - buffersToShrink;
        return targetFree >= 0;
    }

    /// <summary>
    /// Increases the buffer pool size using an optimized algorithm.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void IncreaseBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        System.Int32 threshold = System.Math.Max(1, (System.Int32)(poolInfo.TotalBuffers * 0.25));
        if (poolInfo.FreeBuffers > threshold)
        {
            return;
        }

        System.Double usage = poolInfo.GetUsageRatio();
        System.Double missRatio = poolInfo.GetMissRate();

        System.Int32 increaseStep = CalculateIncreaseStep(in poolInfo, usage, missRatio);
        if (increaseStep <= 0)
        {
            return;
        }

        if (IsOverMemoryBudget())
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Warn($"[{nameof(BufferPoolManager)}] skip-increase size={poolInfo.BufferSize} over budget");
            return;
        }

        if (pool.FreeBuffers > threshold)
        {
            return;
        }

        pool.IncreaseCapacity(increaseStep);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[{nameof(BufferPoolManager)}] " +
                                       $"increase size={poolInfo.BufferSize} by={increaseStep} " +
                                       $"usage={usage:F2} miss={missRatio:F2}");
    }

    /// <summary>
    /// Calculates the increase step based on pool pressure and configuration.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CalculateIncreaseStep(
        in BufferPoolState poolInfo,
        System.Double usage,
        System.Double missRatio)
    {
        System.Int32 baseIncreasePow2 = System.Math.Max(_minIncrease,
            (System.Int32)System.Numerics.BitOperations.RoundUpToPowerOf2(
                (System.UInt32)System.Math.Max(1, poolInfo.TotalBuffers >> 2)));

        System.Double usageFactor = 1.0 + System.Math.Max(0.0, (usage - 0.75) * 2.0);
        System.Double missFactor = 1.0 + System.Math.Min(1.0, missRatio * 2.0);

        System.Int32 scaled = (System.Int32)System.Math.Ceiling(
            baseIncreasePow2 * usageFactor * missFactor * _adaptiveGrowthFactor);

        return System.Math.Min(scaled, _maxIncrease);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Boolean IsOverMemoryBudget()
    {
        System.Int64 totalAvailable = System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        System.Int64 percentBudget = (System.Int64)(totalAvailable * _maxMemoryPct);

        System.Int64 hardCap = _maxMemoryBytes > 0 ? _maxMemoryBytes : (System.Int64)System.Int32.MaxValue * 7;
        System.Int64 targetBudget = System.Math.Min(percentBudget, hardCap);

        System.Int64 current = 0;
        foreach (var pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
            current += info.TotalBuffers * (System.Int64)info.BufferSize;
        }

        return current >= targetBudget;
    }

    #endregion Private: Resize Strategies

    #region Private: Reporting

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void AppendReportHeader(System.Text.StringBuilder sb)
    {
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
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void AppendReportPoolDetails(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine("Pool Details:");
        _ = sb.AppendLine("----------------------------------------------------------------------");
        _ = sb.AppendLine("Size     | Total Buffers | Free Buffers | In Use | Usage % | MissRate");
        _ = sb.AppendLine("----------------------------------------------------------------------");

        foreach (var pool in System.Linq.Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            System.Int32 inUse = info.TotalBuffers - info.FreeBuffers;
            System.Double usage = info.GetUsageRatio() * 100.0;
            System.Double miss = info.GetMissRate() * 100.0;

            _ = sb.AppendLine($"{info.BufferSize,8} | {info.TotalBuffers,13} | {info.FreeBuffers,12} | {inUse,6} | {usage,7:F2}% | {miss,7:F2}%");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
    }

    #endregion Private: Reporting

    #region Parsing

    /// <summary>
    /// Parses the buffer allocation settings with caching for repeated configurations.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
        _suitablePoolSizeCache.Clear();
        _poolManager.Dispose();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(BufferPoolManager)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}