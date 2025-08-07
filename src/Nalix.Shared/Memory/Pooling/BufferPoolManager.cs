using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection.DI;
using Nalix.Shared.Memory.Buffers;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Memory.Pooling;

/// <summary>
/// Manages buffers of various sizes with optimized allocation and deallocation.
/// </summary>
public sealed class BufferPoolManager : SingletonBase<BufferPoolManager>, IBufferPool, IDisposable
{
    #region Constants

    private const Int32 MinimumIncrease = 4;
    private const Int32 MaxBufferIncreaseLimit = 1024;

    #endregion Constants

    #region Fields

    private readonly ILogger? _logger;
    private readonly Int32 _totalBuffers;
    private readonly Boolean _enableTrimming;
    private readonly BufferPoolCollection _poolManager = new();
    private readonly (Int32 BufferSize, Double Allocation)[] _bufferAllocations;
    private readonly ConcurrentDictionary<Int32, Int32> _suitablePoolSizeCache = new();

    // Caches allocation patterns for better performance
    private static readonly ConcurrentDictionary<String, (Int32, Double)[]> _allocationPatternCache = new();

    private Boolean _isInitialized;
    private Int32 _trimCycleCount;
    private Timer? _trimTimer;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the largest buffer size from the buffer allocations list.
    /// </summary>
    public Int32 MaxBufferSize { get; }

    /// <summary>
    /// Gets the smallest buffer size from the buffer allocations list.
    /// </summary>
    public Int32 MinBufferSize { get; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with improved performance.
    /// </summary>
    public BufferPoolManager(BufferConfig? bufferConfig = null, ILogger? logger = null)
    {
        BufferConfig config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferConfig>();

        _logger = logger;
        _totalBuffers = config.TotalBuffers;
        _enableTrimming = config.EnableMemoryTrimming;

        // Parse allocations just once and cache them
        _bufferAllocations = ParseBufferAllocations(config.BufferAllocations);

        // Caches min/max sizes to avoid LINQ in hot paths
        MinBufferSize = _bufferAllocations.Min(alloc => alloc.BufferSize);
        MaxBufferSize = _bufferAllocations.Max(alloc => alloc.BufferSize);

        _poolManager.EventShrink += ShrinkBufferPoolSize;
        _poolManager.EventIncrease += IncreaseBufferPoolSize;

        this.AllocateBuffers();

        // Optional memory trimming timer
        if (_enableTrimming)
        {
            _trimTimer = new Timer(TrimExcessBuffers, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        }
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Periodically trims excess buffers to reduce memory footprint
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrimExcessBuffers(Object? state)
    {
        // Only run deep trimming every 6 cycles (30 minutes with default timer)
        Boolean deepTrim = Interlocked.Increment(ref _trimCycleCount) % 6 == 0;

        _logger?.Info($"Running automatic buffer trimming (Deep trim: {deepTrim})");

        // TODO: Implement trimming logic based on buffer pool statistics
    }

    /// <summary>
    /// Allocates buffers based on the configuration settings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AllocateBuffers()
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException("Buffers already allocated.");
        }

        foreach (var (bufferSize, allocation) in _bufferAllocations)
        {
            Int32 capacity = Math.Max(1, (Int32)(_totalBuffers * allocation));
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Rents a buffer of at least the requested size with optimized caching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Byte[] Rent(Int32 size = 256)
    {
        // Fast path for exact matches to common sizes
        if (size is 256 or 512 or 1024 or 2048 or 4096)
        {
            return _poolManager.RentBuffer(size);
        }

        // Use size cache for frequent sizes
        if (_suitablePoolSizeCache.TryGetValue(size, out Int32 cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            Byte[] buffer = _poolManager.RentBuffer(size);

            // Update cache for this size if it's within reasonable limits
            if (size > 64 && size < 1_000_000 && _suitablePoolSizeCache.Count < 1000)
            {
                _ = _suitablePoolSizeCache.TryAdd(size, buffer.Length);
            }

            return buffer;
        }
        catch (ArgumentException ex)
        {
            _logger?.Error($"Failed to rent buffer of size {size}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Returns the buffer to the appropriate pool with safety checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(Byte[]? buffer)
    {
        if (buffer == null)
        {
            return;
        }

        try
        {
            _poolManager.ReturnBuffer(buffer);
        }
        catch (ArgumentException ex)
        {
            // Log but don't throw to avoid crashing application
            _logger?.Warn($"Failed to return buffer of size {buffer.Length}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    public Double GetAllocationForSize(Int32 size)
    {
        // Optimize common cases with direct comparison
        if (size > MaxBufferSize)
        {
            return _bufferAllocations.Last().Allocation;
        }

        if (size <= MinBufferSize)
        {
            return _bufferAllocations.First().Allocation;
        }

        // Binary search implementation for better performance with many allocations
        Int32 left = 0;
        Int32 right = _bufferAllocations.Length - 1;

        while (left <= right)
        {
            Int32 mid = left + ((right - left) / 2);
            Int32 midSize = _bufferAllocations[mid].BufferSize;

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

        // If we're here, size is between two allocation sizes
        // Return the allocation for the next larger buffer size
        return left < _bufferAllocations.Length ? _bufferAllocations[left].Allocation
                                                : _bufferAllocations.Last().Allocation;
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Parses the buffer allocation settings with caching for repeated configurations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Int32, Double)[] ParseBufferAllocations(String bufferAllocationsString)
    {
        if (String.IsNullOrWhiteSpace(bufferAllocationsString))
        {
            throw new ArgumentException(
                "The input string must not be blank or contain only white spaces.",
                nameof(bufferAllocationsString));
        }

        // Use cached allocations if available for this string
        return _allocationPatternCache.GetOrAdd(bufferAllocationsString, key =>
        {
            try
            {
                var allocations = key
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(pair =>
                    {
                        String[] parts = pair.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        return parts.Length != 2
                            ? throw new FormatException(
                                $"Incorrectly formatted pair: '{pair}'. " +
                                $"Expected format: '<size>,<ratio>;<size>,<ratio>' (e.g., '1024,0.40;2048,0.60').")
                            : !Int32.TryParse(parts[0].Trim(), out Int32 allocationSize) || allocationSize <= 0
                            ? throw new ArgumentOutOfRangeException(
                                nameof(bufferAllocationsString), "Buffers allocation size must be greater than zero.")
                            : !Double.TryParse(parts[1].Trim(), out Double ratio) || ratio <= 0 || ratio > 1
                            ? throw new ArgumentOutOfRangeException(
                                nameof(bufferAllocationsString), "Ratio must be between 0 and 1.")
                            : (allocationSize, ratio);
                    })
                    .OrderBy(tuple => tuple.allocationSize)
                    .ToArray();

                // Validate total allocation doesn't exceed 1.0
                Double totalAllocation = allocations.Sum(a => a.ratio);
                return totalAllocation > 1.1
                    ? throw new ArgumentException(
                        $"Total allocation ratio ({totalAllocation:F2}) exceeds 1.0. " +
                        "The sum of all allocations should be at most 1.0.")
                    : ((Int32, Double)[])allocations;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or
                                     OverflowException or ArgumentOutOfRangeException)
            {
                throw new ArgumentException(
                    "The input string is malformed or contains invalid values. " +
                    $"Expected format: '<size>,<ratio>;<size>,<ratio>' (e.g., '1024,0.40;2048,0.60'). Error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Shrinks the buffer pool size using an optimized algorithm.
    /// </summary>
    /// <param name="pool">The buffer pool to shrink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ShrinkBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        Double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        Int32 targetBuffers = (Int32)(targetAllocation * _totalBuffers);

        // At least 25% of original size to avoid excessive shrinking
        Int32 minimumBuffers = Math.Max(1, poolInfo.TotalBuffers >> 2);

        Int32 excessBuffers = poolInfo.FreeBuffers - targetBuffers;

        // Push safety margin based on pool size to avoid frequent resizing
        // Square root scaling for safety margin
        Int32 safetyMargin = (Int32)Math.Min(20, Math.Sqrt(minimumBuffers));

        Int32 buffersToShrink = Math.Clamp(excessBuffers - safetyMargin, 0, 20);

        if (buffersToShrink > 0)
        {
            // Use lightweight synchronization for better performance
            var lockTaken = false;
            var spinLock = new SpinLock(false);

            try
            {
                spinLock.Enter(ref lockTaken);

                // Double-check after acquiring lock
                if (pool.FreeBuffers > targetBuffers + safetyMargin)
                {
                    pool.DecreaseCapacity(buffersToShrink);

                    _logger?.Info(
                        $"Optimized buffer pool for size {poolInfo.BufferSize}, " +
                        $"reduced by {buffersToShrink}, " +
                        $"new capacity: {poolInfo.TotalBuffers - buffersToShrink}.");
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
    /// <param name="pool">The buffer pool to increase.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncreaseBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        // 25% threshold for adaptive resizing
        Int32 threshold = Math.Max(1, poolInfo.TotalBuffers >> 2);

        if (poolInfo.FreeBuffers <= threshold)
        {
            // Calculate optimal increase amount using power-of-two rounding
            // This helps with memory alignment and predictable growth patterns
            Int32 baseIncrease = Math.Max(
                MinimumIncrease,
                (Int32)BitOperations.RoundUpToPowerOf2((UInt32)Math.Max(1, poolInfo.TotalBuffers >> 2))
            );

            // Apply pool-specific scaling based on miss rate
            Double missRatio = poolInfo.Misses / (Double)Math.Max(1, poolInfo.TotalBuffers);
            Int32 scaledIncrease = missRatio > 0.5
                ? baseIncrease * 2  // Double growth for high-demand pools
                : baseIncrease;

            // Limit the increase to avoid excessive memory usage
            Int32 maxIncrease = Math.Min(
                scaledIncrease,
                MaxBufferIncreaseLimit
            );

            var lockTaken = false;
            var spinLock = new SpinLock(false);

            try
            {
                spinLock.Enter(ref lockTaken);

                // Double-check condition after lock to avoid race conditions
                if (pool.FreeBuffers <= threshold)
                {
                    pool.IncreaseCapacity(maxIncrease);

                    _logger?.Info(
                        $"Optimized buffer pool for size {poolInfo.BufferSize}, " +
                        $"added {maxIncrease} buffers, " +
                        $"new capacity: {poolInfo.TotalBuffers + maxIncrease}, " +
                        $"miss ratio: {missRatio:F2}.");
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

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases all resources of the buffer pools.
    /// </summary>
    protected override void Dispose(System.Boolean disposeManaged)
    {
        // Stop the trimming timer if enabled
        _trimTimer?.Dispose();
        _trimTimer = null;

        // Dispose optimization caches
        _suitablePoolSizeCache.Clear();

        // Dispose the pool manager
        _poolManager.Dispose();

        base.Dispose(disposeManaged);
    }

    #endregion IDisposable
}