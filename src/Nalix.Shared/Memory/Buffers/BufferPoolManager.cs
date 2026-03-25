// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Shared.Memory.Internal.Buffers;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages buffers of various sizes with optimized allocation/deallocation and optional trimming.
/// Includes safety guardrails to prevent aggressive shrinking and ensure minimum buffer availability.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public sealed class BufferPoolManager : System.IDisposable, IReportable
{
    #region Fields & Constants

    private readonly BufferConfig _config;

    private readonly (int BufferSize, double Allocation)[] _bufferAllocations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _suitablePoolSizeCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, BufferPoolMetrics> _metricsCache;

    private readonly System.Buffers.ArrayPool<byte> _fallbackArrayPool = System.Buffers.ArrayPool<byte>.Shared;
    private readonly BufferPoolCollection _poolManager;
    private readonly ShrinkSafetyPolicy _shrinkPolicy;

    private int _trimCycleCount;
    private bool _isInitialized;
    private long _cachedMemoryBudget;
    private long _lastBudgetComputeTime;

    #endregion Fields & Constants

    #region Nested Types

    /// <summary>
    /// Safety policy for shrinking operations to prevent aggressive buffer reduction.
    /// </summary>
    private sealed class ShrinkSafetyPolicy
    {
        /// <summary>
        /// Minimum percentage of total buffers to retain (default 25% = safety margin).
        /// </summary>
        public double MinimumRetentionPercent { get; set; } = 0.25;

        /// <summary>
        /// Maximum buffers to shrink in a single operation (prevent sudden drops).
        /// </summary>
        public int MaxSingleShrinkStep { get; set; } = 20;

        /// <summary>
        /// Maximum percentage of total buffers to shrink per trim cycle (e.g., 20% max per 5min).
        /// </summary>
        public double MaxShrinkPercentPerCycle { get; set; } = 0.20;

        /// <summary>
        /// Minimum absolute buffers per pool (at least 1 for emergency situations).
        /// </summary>
        public int AbsoluteMinimum { get; set; } = 1;
    }

    /// <summary>
    /// Metrics for tracking shrink/expand operations on a pool.
    /// </summary>
    private struct BufferPoolMetrics
    {
        /// <summary>
        /// Total bytes returned to ArrayPool via shrinking.
        /// </summary>
        public long TotalBytesReturned;

        /// <summary>
        /// Number of successful shrink operations.
        /// </summary>
        public int ShrinkAttempted;

        /// <summary>
        /// Number of shrinks skipped due to safety checks.
        /// </summary>
        public int ShrinkSkipped;

        /// <summary>
        /// Number of successful expand operations.
        /// </summary>
        public int ExpandAttempted;

        /// <summary>
        /// Last timestamp when pool state changed.
        /// </summary>
        public long LastChangeTime;
    }

    #endregion Nested Types

    #region Properties

    /// <summary>Gets the largest buffer size from the buffer allocations list.</summary>
    public int MaxBufferSize { get; }

    /// <summary>Gets the smallest buffer size from the buffer allocations list.</summary>
    public int MinBufferSize { get; }

    /// <summary>
    /// Gets the recurring name used for buffer trimming operations.
    /// </summary>
    public static readonly string RecurringName;

    #endregion Properties

    #region Constructors

    static BufferPoolManager() => RecurringName = "buf.trim";

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with validation and trimming.
    /// </summary>
    public BufferPoolManager() : this(bufferConfig: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with validation and trimming.
    /// </summary>
    /// <param name="bufferConfig"></param>
    public BufferPoolManager(BufferConfig? bufferConfig = null)
    {
        BufferConfig config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferConfig>();
        config.Validate();

        _config = config;

        _suitablePoolSizeCache = new();
        _metricsCache = new();
        _shrinkPolicy = new ShrinkSafetyPolicy();

        _bufferAllocations = BufferConfig.ParseBufferAllocations(config.BufferAllocations);

        MinBufferSize = System.Linq.Enumerable.Min(_bufferAllocations, alloc => alloc.BufferSize);
        MaxBufferSize = System.Linq.Enumerable.Max(_bufferAllocations, alloc => alloc.BufferSize);

        _poolManager = new BufferPoolCollection(bufferConfig: config);
        _poolManager.EventShrink += SHRINK_BUFFER_POOL_SIZE;
        _poolManager.EventIncrease += INCREASE_BUFFER_POOL_SIZE;

        ALLOCATE_BUFFERS();

        if (_config.EnableMemoryTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName, GetHashCode()),
                interval: System.TimeSpan.FromMinutes(System.Math.Max(1, _config.TrimIntervalMinutes)),
                work: _ =>
                {
                    TRIM_EXCESS_BUFFERS(null);
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
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
    /// <param name="minimumLength"></param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public byte[] Rent([System.Diagnostics.CodeAnalysis.NotNull] int minimumLength = 256)
    {
        if (IS_FAST_COMMON_SIZE(minimumLength))
        {
            return _poolManager.RentBuffer(minimumLength);
        }

        if (_suitablePoolSizeCache.TryGetValue(minimumLength, out int cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            return RENT_FROM_POOLS_WITH_CACHING(minimumLength);
        }
        catch (System.ArgumentException ex)
        {
            return HANDLE_RENT_FAILURE(minimumLength, ex);
        }
    }

    /// <summary>
    /// Returns the buffer to the appropriate pool with safety checks and fallback.
    /// </summary>
    /// <param name="array"></param>
    /// <param name="arrayClear"></param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public void Return(byte[]? array, bool arrayClear = false)
    {
        if (array is null)
        {
            return;
        }

        try
        {
            RETURN_TO_MANAGED_POOLS(array);
        }
        catch (System.ArgumentException ex)
        {
            HANDLE_RETURN_FAILURE(array, ex);
        }
    }

    /// <summary>
    /// Rents a buffer and wraps it as an <see cref="System.ArraySegment{T}"/> for use with
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> (SAEA) workflows.
    /// </summary>
    /// <param name="size">The minimum number of bytes required.</param>
    /// <returns>
    /// An <see cref="System.ArraySegment{T}"/> backed by a pooled buffer,
    /// with <c>Offset = 0</c> and <c>Count = size</c>.
    /// </returns>
    /// <remarks>
    /// The caller must return the underlying array via <see cref="Return(System.ArraySegment{byte})"/>
    /// or <see cref="ReturnFromSaea"/> after use to avoid leaking pool buffers.
    /// </remarks>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.ArraySegment<byte> RentSegment(int size = 256)
    {
        byte[] buffer = Rent(size);
        return new System.ArraySegment<byte>(buffer, 0, size);
    }

    /// <summary>
    /// Returns a buffer that was rented via <see cref="RentSegment"/> back to the pool.
    /// Only the underlying <see cref="System.ArraySegment{T}.Array"/> is returned;
    /// <c>Offset</c> and <c>Count</c> are ignored.
    /// </summary>
    /// <param name="segment">The segment whose backing array will be returned.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return(System.ArraySegment<byte> segment) => Return(segment.Array);

    /// <summary>
    /// Rents a buffer from the pool and assigns it to the given
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> via
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs.SetBuffer(byte[], int, int)"/>.
    /// </summary>
    /// <param name="saea">The <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> to configure.</param>
    /// <param name="size">The minimum buffer size required.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="saea"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Call <see cref="ReturnFromSaea"/> when the async operation completes to return the buffer to the pool.
    /// </remarks>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RentForSaea(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.SocketAsyncEventArgs saea,
        int size = 256)
    {
        System.ArgumentNullException.ThrowIfNull(saea);

        byte[] buffer = Rent(size);
        saea.SetBuffer(buffer, 0, size);
    }

    /// <summary>
    /// Returns the buffer currently assigned to a
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> back to the pool
    /// and clears the buffer reference on the SAEA.
    /// </summary>
    /// <param name="saea">The <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> whose buffer will be returned.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="saea"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ReturnFromSaea(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.SocketAsyncEventArgs saea)
    {
        System.ArgumentNullException.ThrowIfNull(saea);

        // Grab the buffer before clearing the reference
        byte[]? buffer = saea.Buffer;

        // Detach buffer from SAEA to avoid accidental reuse after return
        saea.SetBuffer(null, 0, 0);

        Return(buffer);
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    /// <param name="size"></param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public double GetAllocationForSize([System.Diagnostics.CodeAnalysis.NotNull] int size)
    {
        if (size > MaxBufferSize)
        {
            return System.Linq.Enumerable.Last(_bufferAllocations).Allocation;
        }

        if (size <= MinBufferSize)
        {
            return System.Linq.Enumerable.First(_bufferAllocations).Allocation;
        }

        int left = 0;
        int right = _bufferAllocations.Length - 1;

        while (left <= right)
        {
            int mid = left + ((right - left) / 2);
            int midSize = _bufferAllocations[mid].BufferSize;

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
    /// Generates a report on the current state of the buffer pools with metrics.
    /// </summary>
    /// <returns>A string containing the report.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public string GenerateReport()
    {
        System.Text.StringBuilder sb = new();

        APPEND_REPORT_HEADER(sb);
        APPEND_REPORT_POOL_DETAILS(sb);
        APPEND_REPORT_METRICS(sb);

        return sb.ToString();
    }

    #endregion Public API

    #region Private: Rent / Return helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IS_FAST_COMMON_SIZE(int size) => size is 256 or 512 or 1024 or 2048 or 4096;

    /// <summary>
    /// Rents buffer from configured pools and optionally updates size cache.
    /// </summary>
    /// <param name="size"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private byte[] RENT_FROM_POOLS_WITH_CACHING(int size)
    {
        byte[] buffer = _poolManager.RentBuffer(size);

        if (_config.EnableAnalytics)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(BufferPoolManager)}:Internal] rent-fast minimumLength={size}");
        }

        CACHE_SUITABLE_POOL_SIZE(size, buffer.Length);

        return buffer;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CACHE_SUITABLE_POOL_SIZE(int requestedSize, int actualSize)
    {
        if (requestedSize is <= 64 or >= 1_000_000)
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
    /// <param name="size"></param>
    /// <param name="ex"></param>
    /// <exception cref="System.ArgumentException"></exception>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private byte[] HANDLE_RENT_FAILURE(int size, System.ArgumentException ex)
    {
        if (_config.FallbackToArrayPool)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SH.{nameof(BufferPoolManager)}:Internal] fallback minimumLength={size} msg={ex.Message}");

            return _fallbackArrayPool.Rent(size);
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error($"[SH.{nameof(BufferPoolManager)}:Internal] rent-fail minimumLength={size} msg={ex.Message}", ex);
        throw ex;
    }

    /// <summary>
    /// Returns a buffer to managed Nalix pools and emits analytics if enabled.
    /// </summary>
    /// <param name="buffer"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void RETURN_TO_MANAGED_POOLS(byte[] buffer)
    {
        _poolManager.ReturnBuffer(buffer);

        if (_config.EnableAnalytics)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] return minimumLength={buffer.Length}");
        }
    }

    /// <summary>
    /// Handles return failure by optionally returning buffer to fallback ArrayPool.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="ex"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void HANDLE_RETURN_FAILURE(byte[] buffer, System.ArgumentException ex)
    {
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (_config.FallbackToArrayPool)
        {
            if (_config.SecureClear)
            {
                System.Array.Clear(buffer, 0, buffer.Length);
            }

            _fallbackArrayPool.Return(buffer, clearArray: false);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SH.{nameof(BufferPoolManager)}:Internal] return-fallback minimumLength={buffer.Length}");

            return;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Warn($"[SH.{nameof(BufferPoolManager)}:Internal] return-fail minimumLength={buffer.Length} msg={ex.Message}");
    }

    #endregion Private: Rent / Return helpers

    #region Private: Allocation & Trimming

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ALLOCATE_BUFFERS()
    {
        if (_isInitialized)
        {
            return;
        }

        foreach ((int bufferSize, double allocation) in _bufferAllocations)
        {
            int capacity = System.Math.Max(1, (int)(_config.TotalBuffers * allocation));
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                      $"init-ok total={_config.TotalBuffers} pools={_bufferAllocations.Length} min={MinBufferSize} max={MaxBufferSize}");
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void TRIM_EXCESS_BUFFERS(object? _)
    {
        int cycle = System.Threading.Interlocked.Increment(ref _trimCycleCount);
        bool deepTrim = SHOULD_RUN_DEEP_TRIM(cycle);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] trim-run deep={deepTrim}");

        // Compute memory budget once per cycle (cache it)
        (long targetBudget, long currentUsage, bool overBudget) = COMPUTE_MEMORY_BUDGET();

        foreach (BufferPoolShared pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            // Only consider TRIM logic, NOT auto-expand events (separate concern)
            if (!SHOULD_TRIM_POOL(in info, overBudget, deepTrim))
            {
                continue;
            }

            int shrinkStep = CALCULATE_SAFE_SHRINK_STEP(in info, cycle);
            if (shrinkStep <= 0)
            {
                continue;
            }

            TRIM_SINGLE_POOL(pool, in info, shrinkStep);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool SHOULD_RUN_DEEP_TRIM(int cycle)
    {
        int deepEvery = System.Math.Max(1, _config.DeepTrimIntervalMinutes / System.Math.Max(1, _config.TrimIntervalMinutes));
        return (cycle % deepEvery) == 0;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private (long TargetBudget, long CurrentUsage, bool OverBudget) COMPUTE_MEMORY_BUDGET()
    {
        // Cache memory budget for 10 seconds to avoid repeated computation
        long now = System.Environment.TickCount64;
        const long CacheDurationMs = 10_000;

        if (now - _lastBudgetComputeTime < CacheDurationMs && _cachedMemoryBudget > 0)
        {
            // Return cached value
            long current = 0;
            foreach (BufferPoolShared pool in _poolManager.GetAllPools())
            {
                ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
                current += info.TotalBuffers * (long)info.BufferSize;
            }

            return (_cachedMemoryBudget, current, current > _cachedMemoryBudget);
        }

        long totalAvailable = System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        long percentBudget = (long)(totalAvailable * _config.MaxMemoryPercentage);
        long hardCap = _config.MaxMemoryBytes > 0 ? _config.MaxMemoryBytes : long.MaxValue;

        long targetBudget = System.Math.Min(percentBudget, hardCap);

        _lastBudgetComputeTime = now;
        _cachedMemoryBudget = targetBudget;

        long currentUsage = 0;
        foreach (BufferPoolShared pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
            currentUsage += info.TotalBuffers * (long)info.BufferSize;
        }

        return (targetBudget, currentUsage, currentUsage > targetBudget);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_TRIM_POOL(in BufferPoolState info, bool overBudget, bool deepTrim)
    {
        // Skip if very low idle time
        double usage = info.GetUsageRatio();
        if (usage > 0.95 && !overBudget && !deepTrim)
        {
            return false;
        }

        bool candidateByFree = info.FreeBuffers >= (int)(info.TotalBuffers * 0.50);
        bool candidateByOverBudget = overBudget || deepTrim;

        return candidateByFree || candidateByOverBudget;
    }

    /// <summary>
    /// Calculates shrink step with safety guardrails to prevent aggressive reduction.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="cycle"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private int CALCULATE_SAFE_SHRINK_STEP(in BufferPoolState info, int cycle)
    {
        if (info.TotalBuffers <= 0)
        {
            return 0;
        }

        // 1. Calculate target based on allocation ratio
        double targetAllocation = GetAllocationForSize(info.BufferSize);
        int targetBuffers = (int)System.Math.Max(
            _shrinkPolicy.AbsoluteMinimum,
            targetAllocation * _config.TotalBuffers
        );

        // 2. Enforce minimum retention percentage (default 25%)
        int minimumRetain = (int)System.Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = System.Math.Max(targetBuffers, minimumRetain);

        // 3. Calculate excess buffers
        int excessBuffers = info.FreeBuffers - targetBuffers;
        if (excessBuffers <= 0)
        {
            return 0;
        }

        // 4. Apply multiple safety limits
        int maxPerCycle = (int)System.Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MaxShrinkPercentPerCycle
        );

        int shrinkStep = System.Math.Min(excessBuffers, maxPerCycle);
        shrinkStep = System.Math.Min(shrinkStep, _shrinkPolicy.MaxSingleShrinkStep);

        return System.Math.Max(0, shrinkStep);
    }

    /// <summary>
    /// Applies trim on a single pool with metrics tracking.
    /// </summary>
    /// <param name="pool"></param>
    /// <param name="info"></param>
    /// <param name="shrinkStep"></param>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void TRIM_SINGLE_POOL(BufferPoolShared pool, in BufferPoolState info, int shrinkStep)
    {
        double usage = info.GetUsageRatio();

        pool.DecreaseCapacity(shrinkStep);

        // Track metrics
        if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.TotalBytesReturned += (long)shrinkStep * info.BufferSize;
            metrics.ShrinkAttempted++;
            metrics.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[info.BufferSize] = metrics;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                       $"trim-shrink minimumLength={info.BufferSize} step={shrinkStep} usage={usage:F2}% " +
                                       $"remain={info.TotalBuffers - shrinkStep}");
    }

    #endregion Private: Allocation & Trimming

    #region Private: Resize Strategies

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool IS_OVER_MEMORY_BUDGET()
    {
        (long _, long _, bool overBudget) = COMPUTE_MEMORY_BUDGET();
        return overBudget;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void SHRINK_BUFFER_POOL_SIZE(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        int buffersToShrink = CALCULATE_AUTO_SHRINK_AMOUNT(in poolInfo);
        if (buffersToShrink <= 0)
        {
            return;
        }

        if (!SHOULD_APPLY_SHRINK(in poolInfo, buffersToShrink))
        {
            if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics metrics))
            {
                metrics.ShrinkSkipped++;
                _metricsCache[poolInfo.BufferSize] = metrics;
            }
            return;
        }

        pool.DecreaseCapacity(buffersToShrink);

        ref readonly BufferPoolState latest = ref pool.GetPoolInfoRef();

        if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics m))
        {
            m.TotalBytesReturned += (long)buffersToShrink * poolInfo.BufferSize;
            m.ShrinkAttempted++;
            m.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = m;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] shrink minimumLength={latest.BufferSize} by={buffersToShrink}");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_AUTO_SHRINK_AMOUNT(in BufferPoolState poolInfo)
    {
        if (poolInfo.TotalBuffers <= 0)
        {
            return 0;
        }

        double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        int targetBuffers = (int)(targetAllocation * _config.TotalBuffers);

        int minimumRetain = (int)System.Math.Ceiling(
            poolInfo.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = System.Math.Max(targetBuffers, minimumRetain);

        int excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        return System.Math.Clamp(excessBuffers, 0, _config.MaxBufferIncreaseLimit);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void INCREASE_BUFFER_POOL_SIZE(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        int threshold = System.Math.Max(1, (int)(poolInfo.TotalBuffers * 0.25));
        if (poolInfo.FreeBuffers > threshold)
        {
            return;
        }

        double usage = poolInfo.GetUsageRatio();
        double missRatio = poolInfo.GetMissRate();

        int increaseStep = CALCULATE_INCREASE_STEP(in poolInfo, usage, missRatio);
        if (increaseStep <= 0)
        {
            return;
        }

        if (IS_OVER_MEMORY_BUDGET())
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SH.{nameof(BufferPoolManager)}:Internal] skip-increase minimumLength={poolInfo.BufferSize} over budget");
            return;
        }

        if (pool.FreeBuffers > threshold)
        {
            return;
        }

        pool.IncreaseCapacity(increaseStep);

        if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.ExpandAttempted++;
            metrics.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = metrics;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                       $"increase minimumLength={poolInfo.BufferSize} by={increaseStep} usage={usage:F2}% miss={missRatio:F2}%");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_INCREASE_STEP(in BufferPoolState poolInfo, double usage, double missRatio)
    {
        int baseIncreasePow2 = System.Math.Max(_config.MinimumIncrease,
                (int)System.Numerics.BitOperations.RoundUpToPowerOf2(
                    (uint)System.Math.Max(1, poolInfo.TotalBuffers >> 2)));

        double usageFactor = 1.0 + System.Math.Max(0.0, (usage - 0.75) * 2.0);
        double missFactor = 1.0 + System.Math.Min(1.0, missRatio * 2.0);

        int scaled = (int)System.Math.Ceiling(
            baseIncreasePow2 * usageFactor * missFactor * _config.AdaptiveGrowthFactor);

        // Tính soft cap: tối đa 25% TotalBuffers hiện tại mỗi lần expand
        // Tránh spike lớn khi pool đang nhỏ mà miss rate đột ngột cao
        int softCap = System.Math.Max(
            _config.MinimumIncrease,
            (int)System.Math.Ceiling(poolInfo.TotalBuffers * 0.25));

        return System.Math.Min(scaled, System.Math.Min(softCap, _config.MaxBufferIncreaseLimit));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_APPLY_SHRINK(in BufferPoolState poolInfo, int buffersToShrink) => buffersToShrink > 0 && poolInfo.FreeBuffers >= buffersToShrink;

    #endregion Private: Resize Strategies

    #region Private: Reporting

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_HEADER(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] BufferPoolManager Status:");
        _ = sb.AppendLine($"Initialized: {_isInitialized}");
        _ = sb.AppendLine($"Total Buffers (Configured): {_config.TotalBuffers}");
        _ = sb.AppendLine($"Pools: {_bufferAllocations.Length}");
        _ = sb.AppendLine($"Min Buffer SIZE: {MinBufferSize}");
        _ = sb.AppendLine($"Max Buffer SIZE: {MaxBufferSize}");
        _ = sb.AppendLine($"Enable Trimming: {_config.EnableMemoryTrimming}");
        _ = sb.AppendLine($"Enable Analytics: {_config.EnableAnalytics}");
        _ = sb.AppendLine($"Enable SecureClear: {_config.SecureClear}");
        _ = sb.AppendLine($"Fallback to ArrayPool: {_config.FallbackToArrayPool}");
        _ = sb.AppendLine($"Trim Interval (min): {_config.TrimIntervalMinutes}");
        _ = sb.AppendLine($"Deep Trim Interval (min): {_config.DeepTrimIntervalMinutes}");
        _ = sb.AppendLine($"Trim Cycles Run: {_trimCycleCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Shrink Safety Policy:");
        _ = sb.AppendLine($"  Minimum Retention: {_shrinkPolicy.MinimumRetentionPercent * 100:F1}%");
        _ = sb.AppendLine($"  Max Single Shrink Step: {_shrinkPolicy.MaxSingleShrinkStep}");
        _ = sb.AppendLine($"  Max Shrink Per Cycle: {_shrinkPolicy.MaxShrinkPercentPerCycle * 100:F1}%");
        _ = sb.AppendLine();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_POOL_DETAILS(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine("s_pool Details:");
        _ = sb.AppendLine("----------------------------------------------------------------------");
        _ = sb.AppendLine("SIZE     | Total  | Free   | In Use  | Usage %  | MissRate");
        _ = sb.AppendLine("----------------------------------------------------------------------");

        foreach (BufferPoolShared? pool in System.Linq.Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            int inUse = info.TotalBuffers - info.FreeBuffers;
            double usage = info.GetUsageRatio() * 100.0;
            double miss = info.GetMissRate() * 100.0;

            _ = sb.AppendLine($"{info.BufferSize,8} | {info.TotalBuffers,6} | {info.FreeBuffers,5} | {inUse,7} | {usage,8:F2}% | {miss,7:F2}%");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_METRICS(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine();
        _ = sb.AppendLine("s_pool Metrics (Shrink/Expand Operations):");
        _ = sb.AppendLine("----------------------------------------------------------------------");
        _ = sb.AppendLine("SIZE     | Shrink OK | Shrink Skip | Expand OK | Bytes Returned");
        _ = sb.AppendLine("----------------------------------------------------------------------");

        foreach (BufferPoolShared? pool in System.Linq.Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
            {
                string bytesReturnedStr = metrics.TotalBytesReturned > 1_000_000
                    ? $"{metrics.TotalBytesReturned / 1_000_000}MB"
                    : $"{metrics.TotalBytesReturned / 1024}KB";

                _ = sb.AppendLine($"{info.BufferSize,8} | {metrics.ShrinkAttempted,9} | {metrics.ShrinkSkipped,11} | {metrics.ExpandAttempted,9} | {bytesReturnedStr,14}");
            }
            else
            {
                _ = sb.AppendLine($"{info.BufferSize,8} | {0,9} | {0,11} | {0,9} | {"0KB",14}");
            }
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
    }

    #endregion Private: Reporting

    #region IDisposable

    /// <summary>
    /// Releases all resources of the buffer pools.
    /// </summary>
    public void Dispose()
    {
        _suitablePoolSizeCache.Clear();
        _metricsCache.Clear();
        _poolManager.Dispose();

        if (_config.EnableMemoryTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelRecurring(TaskNaming.Recurring
                                    .CleanupJobId(RecurringName, GetHashCode()));
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(BufferPoolManager)}:{nameof(Dispose)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
