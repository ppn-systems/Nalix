using Notio.Common.Logging.Interfaces;
using Notio.Common.Memory.Pools;
using Notio.Shared.Configuration;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Manages buffers of various sizes and handles dynamic buffer allocation and deallocation.
/// </summary>
public sealed class BufferAllocator : IBufferPool
{
    private const int MinimumIncrease = 4;
    private const int MaxBufferIncreaseLimit = 1024;

    private readonly ILogger? _logger;
    private readonly int _totalBuffers;
    private readonly (int BufferSize, double Allocation)[] _bufferAllocations;
    private readonly BufferManager _poolManager = new();

    private bool _isInitialized;

    /// <summary>
    /// Gets the buffer configuration from the system configuration manager.
    /// </summary>
    /// <value>The buffer configuration set from the system.</value>
    public BufferConfig BufferConfig { get; } = ConfiguredShared.Instance.Get<BufferConfig>();

    /// <summary>
    /// Gets the largest buffer size from the buffer allocations list.
    /// </summary>
    public int MaxBufferSize => _bufferAllocations.Max(alloc => alloc.BufferSize);

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferAllocator"/> class with buffer allocation settings and total buffer count.
    /// </summary>
    /// <param name="bufferConfig">The buffer configuration to use. If null, the default configuration is used.</param>
    public BufferAllocator(BufferConfig? bufferConfig = null, ILogger? logger = null)
    {
        if (bufferConfig is not null)
            BufferConfig = bufferConfig;

        _logger = logger;
        _totalBuffers = BufferConfig.TotalBuffers;
        _bufferAllocations = ParseBufferAllocations(BufferConfig.BufferAllocations);

        _poolManager.EventShrink += ShrinkBufferPoolSize;
        _poolManager.EventIncrease += IncreaseBufferPoolSize;

        this.AllocateBuffers();
    }

    /// <summary>
    /// Allocates buffers based on the configuration settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if buffers have already been allocated.</exception>
    private void AllocateBuffers()
    {
        if (_isInitialized) throw new InvalidOperationException("Buffers already allocated.");

        foreach (var (bufferSize, allocation) in _bufferAllocations)
        {
            int capacity = (int)(_totalBuffers * allocation);
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Rents a buffer of at least the requested size.
    /// </summary>
    /// <param name="size">The size of the buffer to rent. Default is 1024.</param>
    /// <returns>A byte array representing the rented buffer.</returns>
    public byte[] Rent(int size = 1024) => _poolManager.RentBuffer(size);

    /// <summary>
    /// Returns the buffer to the appropriate pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    public void Return(byte[] buffer) => _poolManager.ReturnBuffer(buffer);

    /// <summary>
    /// Gets the allocation ratio for a given buffer size.
    /// </summary>
    /// <param name="size">The size of the buffer.</param>
    /// <returns>The allocation ratio for the given buffer size.</returns>
    /// <exception cref="ArgumentException">Thrown if no allocation ratio is found for the given buffer size.</exception>
    public double GetAllocationForSize(int size)
    {
        foreach (var (bufferSize, allocation) in _bufferAllocations.OrderBy(alloc => alloc.BufferSize))
        {
            if (bufferSize >= size)
                return allocation;
        }

        throw new ArgumentException($"No allocation found for size: {size}");
    }

    /// <summary>
    /// Parses the buffer allocation settings from a configuration string.
    /// </summary>
    /// <param name="bufferAllocationsString">The buffer allocations string in the format '<size>,<ratio>;...'</param>
    /// <returns>An array of tuples representing the buffer size and allocation ratio.</returns>
    /// <exception cref="ArgumentException">Thrown if the input string is malformed.</exception>
    private static (int, double)[] ParseBufferAllocations(string bufferAllocationsString)
    {
        if (string.IsNullOrWhiteSpace(bufferAllocationsString))
        {
            throw new ArgumentException(
                "The input string must not be left blank or contain only white spaces.",
                nameof(bufferAllocationsString));
        }

        try
        {
            return bufferAllocationsString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair =>
                {
                    string[] parts = pair.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        throw new FormatException(
                            $"Incorrectly formatted pairs: '{pair}'. " +
                            $"Expected format: '<size>,<ratio>;<size>,<ratio>' (e.g., '1024,0.40;2048,0.60').");
                    }

                    int allocationSize = int.Parse(parts[0].Trim());
                    double ratio = double.Parse(parts[1].Trim());

                    if (allocationSize <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(bufferAllocationsString), "Buffer allocation size must be greater than zero.");
                    }

                    if (ratio <= 0 || ratio > 1)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(bufferAllocationsString), "Ratio must be between 0 and 1.");
                    }

                    return (allocationSize, ratio);
                })
                .ToArray();
        }
        catch (Exception ex) when (ex is FormatException
                                || ex is ArgumentException
                                || ex is OverflowException
                                || ex is ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                "The input string is malformed or contains invalid values. " +
                $"Expected format: '<size>,<ratio>;<size>,<ratio>' (e.g., '1024,0.40;2048,0.60').");
        }
    }

    /// <summary>
    /// Shrinks the buffer pool size using an optimal algorithm.
    /// </summary>
    /// <param name="pool">The buffer pool to shrink.</param>
    private void ShrinkBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferInfo poolInfo = ref pool.GetPoolInfoRef();

        double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        int targetBuffers = (int)(targetAllocation * _totalBuffers);
        int minimumBuffers = poolInfo.TotalBuffers >> 2;

        int excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        int safetyMargin = Math.Min(20, minimumBuffers);

        int buffersToShrink = Math.Clamp(
            excessBuffers - safetyMargin,
            0,
            20
        );

        if (buffersToShrink > 0)
        {
            SpinLock spinLock = new(false);
            bool lockTaken = false;

            try
            {
                spinLock.Enter(ref lockTaken);
                pool.DecreaseCapacity(buffersToShrink);

                _logger?.Info(
                    $"Shrank buffer pool for size {poolInfo.BufferSize}, " +
                    $"reduced by {buffersToShrink}, " +
                    $"new capacity: {poolInfo.TotalBuffers - buffersToShrink}.");
            }
            finally
            {
                if (lockTaken) spinLock.Exit();
            }
        }
        else
        {
            _logger?.Info(
                $"No buffers were shrunk for pool size {poolInfo.BufferSize}. " +
                $"Current capacity is optimal.");
        }
    }

    /// <summary>
    /// Increases the buffer pool size using an optimal algorithm.
    /// </summary>
    /// <param name="pool">The buffer pool to increase.</param>
    private void IncreaseBufferPoolSize(BufferPoolShared pool)
    {
        ref readonly BufferInfo poolInfo = ref pool.GetPoolInfoRef();

        int threshold = poolInfo.TotalBuffers >> 2; // 25% threshold

        if (poolInfo.FreeBuffers <= threshold)
        {
            // Optimized increase calculation
            int baseIncrease = (int)Math.Max(
                MinimumIncrease,
                BitOperations.RoundUpToPowerOf2((uint)poolInfo.TotalBuffers) >> 2
            );

            // Limit the increase to avoid OOM
            int maxIncrease = Math.Min(
                baseIncrease,
                MaxBufferIncreaseLimit
            );

            SpinLock spinLock = new(false);
            bool lockTaken = false;

            try
            {
                spinLock.Enter(ref lockTaken);

                if (pool.FreeBuffers <= threshold)
                {
                    pool.IncreaseCapacity(maxIncrease);

                    _logger?.Info(
                        $"Increased buffer pool for size {poolInfo.BufferSize}, " +
                        $"added {maxIncrease}, " +
                        $"new capacity: {poolInfo.TotalBuffers + maxIncrease}.");
                }
            }
            finally
            {
                if (lockTaken) spinLock.Exit();
            }
        }
        else
        {
            // Logging
            _logger?.Info(
                $"No increase needed for pool size {poolInfo.BufferSize}. " +
                $"Free buffers: {poolInfo.FreeBuffers}, " +
                $"threshold: {threshold}.");
        }
    }

    /// <summary>
    /// Releases all resources of the buffer pools.
    /// </summary>
    public void Dispose() => _poolManager.Dispose();
}