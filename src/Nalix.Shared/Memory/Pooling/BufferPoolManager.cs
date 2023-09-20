using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection.DI;
using Nalix.Shared.Memory.Buffers;
using System.Linq;

namespace Nalix.Shared.Memory.Pooling;

/// <summary>
/// Manages buffers of various sizes with optimized allocation and deallocation.
/// </summary>
public sealed class BufferPoolManager : SingletonBase<BufferPoolManager>, IBufferPool, System.IDisposable
{
    #region Constants

    private const System.Int32 MinimumIncrease = 4;
    private const System.Int32 MaxBufferIncreaseLimit = 1024;

    #endregion Constants

    #region Fields

    // Caches allocation patterns for better performance
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.String, (System.Int32, System.Double)[]> _allocationPatternCache;

    private readonly ILogger? _logger;
    private readonly System.Int32 _totalBuffers;
    private readonly System.Boolean _enableTrimming;
    private readonly BufferPoolCollection _poolManager = new();
    private readonly (System.Int32 BufferSize, System.Double Allocation)[] _bufferAllocations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, System.Int32> _suitablePoolSizeCache;

    private System.Threading.Timer? _trimTimer;
    private System.Int32 _trimCycleCount;
    private System.Boolean _isInitialized;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the largest buffer size from the buffer allocations list.
    /// </summary>
    public System.Int32 MaxBufferSize { get; }

    /// <summary>
    /// Gets the smallest buffer size from the buffer allocations list.
    /// </summary>
    public System.Int32 MinBufferSize { get; }

    #endregion Properties

    #region Constructors

    static BufferPoolManager() => _allocationPatternCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with improved performance.
    /// </summary>
    public BufferPoolManager(BufferConfig? bufferConfig = null, ILogger? logger = null)
    {
        BufferConfig config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferConfig>();

        _suitablePoolSizeCache = new();

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
            _trimTimer = new System.Threading.Timer(TrimExcessBuffers, null,
                             System.TimeSpan.FromMinutes(1),
                             System.TimeSpan.FromMinutes(5));
        }
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Periodically trims excess buffers to reduce memory footprint
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void TrimExcessBuffers(System.Object? state)
    {
        // Only run deep trimming every 6 cycles (30 minutes with default timer)
        System.Boolean deepTrim = System.Threading.Interlocked.Increment(ref _trimCycleCount) % 6 == 0;

        _logger?.Info($"Running automatic buffer trimming (Deep trim: {deepTrim})");

        // TODO: Implement trimming logic based on buffer pool statistics
    }

    /// <summary>
    /// Allocates buffers based on the configuration settings.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AllocateBuffers()
    {
        if (_isInitialized)
        {
            throw new System.InvalidOperationException("Buffers already allocated.");
        }

        foreach (var (bufferSize, allocation) in _bufferAllocations)
        {
            System.Int32 capacity = System.Math.Max(1, (System.Int32)(_totalBuffers * allocation));
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Rents a buffer of at least the requested size with optimized caching.
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

        // Use size cache for frequent sizes
        if (_suitablePoolSizeCache.TryGetValue(size, out System.Int32 cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            System.Byte[] buffer = _poolManager.RentBuffer(size);

            // Update cache for this size if it's within reasonable limits
            if (size > 64 && size < 1_000_000 && _suitablePoolSizeCache.Count < 1000)
            {
                _ = _suitablePoolSizeCache.TryAdd(size, buffer.Length);
            }

            return buffer;
        }
        catch (System.ArgumentException ex)
        {
            _logger?.Error($"Failed to rent buffer of size {size}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Returns the buffer to the appropriate pool with safety checks.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return(System.Byte[]? buffer)
    {
        if (buffer == null)
        {
            return;
        }

        try
        {
            _poolManager.ReturnBuffer(buffer);
        }
        catch (System.ArgumentException ex)
        {
            // Log but don't throw to avoid crashing application
            _logger?.Warn($"Failed to return buffer of size {buffer.Length}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    public System.Double GetAllocationForSize(System.Int32 size)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static (System.Int32, System.Double)[] ParseBufferAllocations(System.String bufferAllocationsString)
    {
        if (System.String.IsNullOrWhiteSpace(bufferAllocationsString))
        {
            throw new System.ArgumentException(
                "The input string must not be blank or contain only white spaces.",
                nameof(bufferAllocationsString));
        }

        // Use cached allocations if available for this string
        return _allocationPatternCache.GetOrAdd(bufferAllocationsString, key =>
        {
            try
            {
                var allocations = key
                    .Split(';', System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(pair =>
                    {
                        System.String[] parts = pair.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                        return parts.Length != 2
                            ? throw new System.FormatException(
                                $"Incorrectly formatted pair: '{pair}'. " +
                                $"Expected format: '<size>,<ratio>;<size>,<ratio>' (e.g., '1024,0.40;2048,0.60').")
                            : !System.Int32.TryParse(parts[0].Trim(), out System.Int32 allocationSize) || allocationSize <= 0
                            ? throw new System.ArgumentOutOfRangeException(
                                nameof(bufferAllocationsString), "Buffers allocation size must be greater than zero.")
                            : !System.Double.TryParse(parts[1].Trim(), out System.Double ratio) || ratio <= 0 || ratio > 1
                            ? throw new System.ArgumentOutOfRangeException(
                                nameof(bufferAllocationsString), "Ratio must be between 0 and 1.")
                            : (allocationSize, ratio);
                    })
                    .OrderBy(tuple => tuple.allocationSize)
                    .ToArray();

                // Validate total allocation doesn't exceed 1.0
                System.Double totalAllocation = allocations.Sum(a => a.ratio);
                return totalAllocation > 1.1
                    ? throw new System.ArgumentException(
                        $"Total allocation ratio ({totalAllocation:F2}) exceeds 1.0. " +
                        "The sum of all allocations should be at most 1.0.")
                    : ((System.Int32, System.Double)[])allocations;
            }
            catch (System.Exception ex) when (ex is
                System.FormatException or System.ArgumentException or
                System.OverflowException or System.ArgumentOutOfRangeException)
            {
                throw new System.ArgumentException(
                    "The input string is malformed or contains invalid values. " +
                    $"Expected format: '<size>,<ratio>;<size>,<ratio>' (e.g., '1024,0.40;2048,0.60'). Error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Shrinks the buffer pool size using an optimized algorithm.
    /// </summary>
    /// <param name="pool">The buffer pool to shrink.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ShrinkBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        System.Double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        System.Int32 targetBuffers = (System.Int32)(targetAllocation * _totalBuffers);

        // At least 25% of original size to avoid excessive shrinking
        System.Int32 minimumBuffers = System.Math.Max(1, poolInfo.TotalBuffers >> 2);

        System.Int32 excessBuffers = poolInfo.FreeBuffers - targetBuffers;

        // Push safety margin based on pool size to avoid frequent resizing
        // Square root scaling for safety margin
        System.Int32 safetyMargin = (System.Int32)System.Math.Min(20, System.Math.Sqrt(minimumBuffers));

        System.Int32 buffersToShrink = System.Math.Clamp(excessBuffers - safetyMargin, 0, 20);

        if (buffersToShrink > 0)
        {
            // Use lightweight synchronization for better performance
            System.Boolean lockTaken = false;
            System.Threading.SpinLock spinLock = new(false);

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void IncreaseBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        // 25% threshold for adaptive resizing
        System.Int32 threshold = System.Math.Max(1, poolInfo.TotalBuffers >> 2);

        if (poolInfo.FreeBuffers <= threshold)
        {
            // Calculate optimal increase amount using power-of-two rounding
            // This helps with memory alignment and predictable growth patterns
            System.Int32 baseIncrease = System.Math.Max(MinimumIncrease, (System.Int32)System.Numerics.BitOperations
                                                   .RoundUpToPowerOf2((System.UInt32)System.Math
                                                   .Max(1, poolInfo.TotalBuffers >> 2))
            );

            // Apply pool-specific scaling based on miss rate
            System.Double missRatio = poolInfo.Misses / (System.Double)System.Math.Max(1, poolInfo.TotalBuffers);
            System.Int32 scaledIncrease = missRatio > 0.5
                ? baseIncrease * 2  // Double growth for high-demand pools
                : baseIncrease;

            // Limit the increase to avoid excessive memory usage
            System.Int32 maxIncrease = System.Math.Min(
                scaledIncrease,
                MaxBufferIncreaseLimit
            );

            System.Boolean lockTaken = false;
            System.Threading.SpinLock spinLock = new(false);

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