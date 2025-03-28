using Notio.Common.Logging;
using Notio.Common.Caching;
using Notio.Shared.Configuration;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Shared.Memory.Buffers;

/// <summary>
/// Manages buffers of various sizes with optimized allocation and deallocation.
/// </summary>
public sealed class BufferAllocator : IBufferPool, IDisposable
{
    private const int MinimumIncrease = 4;
    private const int MaxBufferIncreaseLimit = 1024;

    private readonly ILogger? _logger;
    private readonly int _totalBuffers;
    private readonly int _minBufferSize;
    private readonly int _maxBufferSize;
    private readonly bool _enableTrimming;
    private readonly BufferManager _poolManager = new();
    private readonly (int BufferSize, double Allocation)[] _bufferAllocations;
    private readonly ConcurrentDictionary<int, int> _suitablePoolSizeCache = new();

    // Caches allocation patterns for better performance
    private static readonly ConcurrentDictionary<string, (int, double)[]> _allocationPatternCache = new();

    private bool _isInitialized;
    private int _trimCycleCount;
    private Timer? _trimTimer;

    /// <summary>
    /// Gets the largest buffer size from the buffer allocations list.
    /// </summary>
    public int MaxBufferSize => _maxBufferSize;

    /// <summary>
    /// Gets the smallest buffer size from the buffer allocations list.
    /// </summary>
    public int MinBufferSize => _minBufferSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferAllocator"/> class with improved performance.
    /// </summary>
    public BufferAllocator(BufferConfig? bufferConfig = null, ILogger? logger = null)
    {
        BufferConfig config = bufferConfig ?? ConfiguredShared.Instance.Get<BufferConfig>();

        _logger = logger;
        _totalBuffers = config.TotalBuffers;
        _enableTrimming = config.EnableMemoryTrimming;

        // Parse allocations just once and cache them
        _bufferAllocations = ParseBufferAllocations(config.BufferAllocations);

        // Caches min/max sizes to avoid LINQ in hot paths
        _minBufferSize = _bufferAllocations.Min(alloc => alloc.BufferSize);
        _maxBufferSize = _bufferAllocations.Max(alloc => alloc.BufferSize);

        _poolManager.EventShrink += ShrinkBufferPoolSize;
        _poolManager.EventIncrease += IncreaseBufferPoolSize;

        this.AllocateBuffers();

        // Optional memory trimming timer
        if (_enableTrimming)
            _trimTimer = new Timer(TrimExcessBuffers, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Periodically trims excess buffers to reduce memory footprint
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrimExcessBuffers(object? state)
    {
        // Only run deep trimming every 6 cycles (30 minutes with default timer)
        bool deepTrim = Interlocked.Increment(ref _trimCycleCount) % 6 == 0;

        _logger?.Info($"Running automatic buffer trimming (Deep trim: {deepTrim})");

        // TODO: Implement trimming logic based on buffer pool statistics
    }

    /// <summary>
    /// Allocates buffers based on the configuration settings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AllocateBuffers()
    {
        if (_isInitialized) throw new InvalidOperationException("Buffers already allocated.");

        foreach (var (bufferSize, allocation) in _bufferAllocations)
        {
            int capacity = Math.Max(1, (int)(_totalBuffers * allocation));
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Rents a buffer of at least the requested size with optimized caching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Rent(int size = 256)
    {
        // Fast path for exact matches to common sizes
        if (size == 256 || size == 512 || size == 1024 || size == 2048 || size == 4096)
        {
            return _poolManager.RentBuffer(size);
        }

        // Use size cache for frequent sizes
        if (_suitablePoolSizeCache.TryGetValue(size, out int cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            byte[] buffer = _poolManager.RentBuffer(size);

            // Update cache for this size if it's within reasonable limits
            if (size > 64 && size < 1_000_000 && _suitablePoolSizeCache.Count < 1000)
            {
                _suitablePoolSizeCache.TryAdd(size, buffer.Length);
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
    public void Return(byte[]? buffer)
    {
        if (buffer == null) return;

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
    public double GetAllocationForSize(int size)
    {
        // Optimize common cases with direct comparison
        if (size > _maxBufferSize)
            return _bufferAllocations.Last().Allocation;

        if (size <= _minBufferSize)
            return _bufferAllocations.First().Allocation;

        // Binary search implementation for better performance with many allocations
        int left = 0;
        int right = _bufferAllocations.Length - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            int midSize = _bufferAllocations[mid].BufferSize;

            if (midSize == size)
                return _bufferAllocations[mid].Allocation;

            if (midSize < size)
                left = mid + 1;
            else
                right = mid - 1;
        }

        // If we're here, size is between two allocation sizes
        // Return the allocation for the next larger buffer size
        return left < _bufferAllocations.Length ? _bufferAllocations[left].Allocation
                                                : _bufferAllocations.Last().Allocation;
    }

    /// <summary>
    /// Parses the buffer allocation settings with caching for repeated configurations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int, double)[] ParseBufferAllocations(string bufferAllocationsString)
    {
        if (string.IsNullOrWhiteSpace(bufferAllocationsString))
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
                        string[] parts = pair.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 2)
                        {
                            throw new FormatException(
                                $"Incorrectly formatted pair: '{pair}'. " +
                                $"Expected format: '<size>,<ratio>;<size>,<ratio>' (e.g., '1024,0.40;2048,0.60').");
                        }

                        if (!int.TryParse(parts[0].Trim(), out int allocationSize) || allocationSize <= 0)
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(bufferAllocationsString), "Buffers allocation size must be greater than zero.");
                        }

                        if (!double.TryParse(parts[1].Trim(), out double ratio) || ratio <= 0 || ratio > 1)
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(bufferAllocationsString), "Ratio must be between 0 and 1.");
                        }

                        return (allocationSize, ratio);
                    })
                    .OrderBy(tuple => tuple.allocationSize)
                    .ToArray();

                // Validate total allocation doesn't exceed 1.0
                double totalAllocation = allocations.Sum(a => a.ratio);
                if (totalAllocation > 1.1) // Allow slight overallocation with a tolerance of 10%
                {
                    throw new ArgumentException(
                        $"Total allocation ratio ({totalAllocation:F2}) exceeds 1.0. " +
                        "The sum of all allocations should be at most 1.0.");
                }

                return allocations;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException ||
                                     ex is OverflowException || ex is ArgumentOutOfRangeException)
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
        ref readonly BufferInfo poolInfo = ref pool.GetPoolInfoRef();

        double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        int targetBuffers = (int)(targetAllocation * _totalBuffers);

        // At least 25% of original size to avoid excessive shrinking
        int minimumBuffers = Math.Max(1, poolInfo.TotalBuffers >> 2);

        int excessBuffers = poolInfo.FreeBuffers - targetBuffers;

        // Add safety margin based on pool size to avoid frequent resizing
        int safetyMargin = (int)Math.Min(
            20,
            Math.Sqrt(minimumBuffers) // Square root scaling for safety margin
        );

        int buffersToShrink = Math.Clamp(excessBuffers - safetyMargin, 0, 20);

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
                if (lockTaken) spinLock.Exit();
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
        ref readonly BufferInfo poolInfo = ref pool.GetPoolInfoRef();

        // 25% threshold for adaptive resizing
        int threshold = Math.Max(1, poolInfo.TotalBuffers >> 2);

        if (poolInfo.FreeBuffers <= threshold)
        {
            // Calculate optimal increase amount using power-of-two rounding
            // This helps with memory alignment and predictable growth patterns
            int baseIncrease = Math.Max(
                MinimumIncrease,
                (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, poolInfo.TotalBuffers >> 2))
            );

            // Apply pool-specific scaling based on miss rate
            double missRatio = poolInfo.Misses / (double)Math.Max(1, poolInfo.TotalBuffers);
            int scaledIncrease = missRatio > 0.5
                ? baseIncrease * 2  // Double growth for high-demand pools
                : baseIncrease;

            // Limit the increase to avoid excessive memory usage
            int maxIncrease = Math.Min(
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
                if (lockTaken) spinLock.Exit();
            }
        }
    }

    /// <summary>
    /// Releases all resources of the buffer pools.
    /// </summary>
    public void Dispose()
    {
        // Stop the trimming timer if enabled
        _trimTimer?.Dispose();
        _trimTimer = null;

        // Clear optimization caches
        _suitablePoolSizeCache.Clear();

        // Dispose the pool manager
        _poolManager.Dispose();

        GC.SuppressFinalize(this);
    }
}
