// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Pools;

/// <summary>
/// Provides a pool of reusable <see cref="System.Collections.Generic.List{T}"/> instances, similar to ArrayPool,
/// optimized for high-performance real-time server scenarios.
/// </summary>
/// <typeparam name="T">The type of elements in the lists.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ListPool{T}"/> class.
/// </remarks>
/// <param name="maxPoolSize">The maximum number of lists to keep in the pool.</param>
/// <param name="initialCapacity">The initial capacity of new lists.</param>
public sealed class ListPool<T>(System.Int32 maxPoolSize, System.Int32 initialCapacity)
{
    #region Constants

    // Configuration constants
    private const System.Int32 DefaultMaxPoolSize = 1024;
    private const System.Int32 MaxInitialCapacity = 8192;
    private const System.Int32 DefaultInitialCapacity = 16;

    #endregion Constants

    #region Fields

    // Thread-safe storage for pooled lists
    private readonly System.Collections.Concurrent.ConcurrentBag<System.Collections.Generic.List<T>> _listBag = [];

    // Statistics tracking
    private System.Int64 _rented;

    private System.Int64 _returned;
    private System.Int64 _created;
    private System.Int64 _trimmed;
    private readonly System.Diagnostics.Stopwatch _uptime = System.Diagnostics.Stopwatch.StartNew();

    private readonly System.Int32 _maxPoolSize = maxPoolSize > 0 ? maxPoolSize : DefaultMaxPoolSize;
    private readonly System.Int32 _initialCapacity = ValidateCapacity(initialCapacity);

    #endregion Fields

    #region Properties

    /// <summary>
    /// Event for trace information.
    /// </summary>
    public event System.Action<System.String>? TraceOccurred;

    /// <summary>
    /// Gets the singleton instance of the <see cref="ListPool{T}"/>.
    /// </summary>
    public static ListPool<T> Instance { get; } = new();

    /// <summary>
    /// Gets the number of lists currently available in the pool.
    /// </summary>
    public System.Int32 AvailableCount => _listBag.Count;

    /// <summary>
    /// Gets the total number of lists created by this pool.
    /// </summary>
    public System.Int64 CreatedCount => System.Threading.Interlocked.Read(ref _created);

    /// <summary>
    /// Gets the number of lists currently rented from the pool.
    /// </summary>
    public System.Int64 RentedCount => System.Threading.Interlocked.Read(ref _rented) - System.Threading.Interlocked.Read(ref _returned);

    /// <summary>
    /// Gets the total number of rent operations performed.
    /// </summary>
    public System.Int64 TotalRentOperations => System.Threading.Interlocked.Read(ref _rented);

    /// <summary>
    /// Gets the total number of return operations performed.
    /// </summary>
    public System.Int64 TotalReturnOperations => System.Threading.Interlocked.Read(ref _returned);

    /// <summary>
    /// Gets the number of lists that have been trimmed from the pool.
    /// </summary>
    public System.Int64 TrimmedCount => System.Threading.Interlocked.Read(ref _trimmed);

    /// <summary>
    /// Gets the uptime of the pool in milliseconds.
    /// </summary>
    public System.Int64 UptimeMs => _uptime.ElapsedMilliseconds;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes the default instance of the <see cref="ListPool{T}"/>.
    /// </summary>
    private ListPool()
        : this(DefaultMaxPoolSize, DefaultInitialCapacity)
    {
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Rents a <see cref="System.Collections.Generic.List{T}"/> instance from the pool.
    /// </summary>
    /// <param name="minimumCapacity">Optional. The minimum capacity required for the list.</param>
    /// <returns>A <see cref="System.Collections.Generic.List{T}"/> instance that can be used and then returned to the pool.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> Rent(System.Int32 minimumCapacity = 0)
    {
        _ = System.Threading.Interlocked.Increment(ref _rented);

        // Try to get a list from the pool
        if (_listBag.TryTake(out System.Collections.Generic.List<T>? list))
        {
            // If a minimum capacity is specified, ensure the list meets it
            if (minimumCapacity > 0 && list.Capacity < minimumCapacity)
            {
                list.Capacity = minimumCapacity;
            }

            return list;
        }

        // Create a new list with appropriate capacity
        System.Int32 capacity = minimumCapacity > 0 ? System.Math.Max(minimumCapacity, _initialCapacity) : _initialCapacity;
        System.Collections.Generic.List<T> newList = new(capacity);

        _ = System.Threading.Interlocked.Increment(ref _created);
        TraceOccurred?.Invoke($"Rent(): Created new List<{typeof(T).Name}> instance (Capacity={capacity}, TotalCreated={CreatedCount})");

        return newList;
    }

    /// <summary>
    /// Returns a <see cref="System.Collections.Generic.List{T}"/> to the pool.
    /// </summary>
    /// <param name="list">The list to return to the pool.</param>
    /// <param name="clearItems">Whether to clear the list before returning it. Standard is true.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return(System.Collections.Generic.List<T> list, System.Boolean clearItems = true)
    {
        System.ArgumentNullException.ThrowIfNull(list);

        _ = System.Threading.Interlocked.Increment(ref _returned);

        // Dispose the list if requested
        if (clearItems)
        {
            list.Clear();
        }

        // Only add to the pool if we haven't reached the maximum size
        if (_listBag.Count < _maxPoolSize)
        {
            _listBag.Add(list);
        }
        else
        {
            // The list will be garbage collected since we're not storing it
            _ = System.Threading.Interlocked.Increment(ref _trimmed);
            TraceOccurred?.Invoke($"Return(): Pools size limit reached, discarding list (MaxSize={_maxPoolSize}, Trimmed={TrimmedCount})");
        }
    }

    /// <summary>
    /// Creates and initializes multiple lists in the pool.
    /// </summary>
    /// <param name="count">The number of lists to preallocate.</param>
    /// <param name="capacity">The capacity for each preallocated list.</param>
    public void Prealloc(System.Int32 count, System.Int32 capacity = 0)
    {
        if (count <= 0)
        {
            return;
        }

        System.Int32 listCapacity = capacity > 0 ? capacity : _initialCapacity;
        System.Int32 preallocationCount = System.Math.Min(count, _maxPoolSize - _listBag.Count);

        if (preallocationCount <= 0)
        {
            return;
        }

        for (System.Int32 i = 0; i < preallocationCount; i++)
        {
            System.Collections.Generic.List<T> list = new(listCapacity);
            _listBag.Add(list);
            _ = System.Threading.Interlocked.Increment(ref _created);
        }

        TraceOccurred?.Invoke($"Prealloc(): Created {preallocationCount} List<{typeof(T).Name}> instances (Capacity={listCapacity})");
    }

    /// <summary>
    /// Trims the pool to a specified size.
    /// </summary>
    /// <param name="maximumSize">The maximum number of lists to keep in the pool.</param>
    /// <returns>The number of lists removed from the pool.</returns>
    public System.Int32 Trim(System.Int32 maximumSize = 0)
    {
        System.Int32 targetSize = maximumSize > 0 ? maximumSize : _maxPoolSize / 2;
        System.Int32 trimCount = 0;

        while (_listBag.Count > targetSize && _listBag.TryTake(out _))
        {
            trimCount++;
        }

        if (trimCount > 0)
        {
            _ = System.Threading.Interlocked.Add(ref _trimmed, trimCount);
            TraceOccurred?.Invoke($"Trim(): Removed {trimCount} List<{typeof(T).Name}> instances from pool");
        }

        return trimCount;
    }

    /// <summary>
    /// Clears all lists from the pool.
    /// </summary>
    /// <returns>The number of lists removed from the pool.</returns>
    public System.Int32 Clear()
    {
        System.Int32 count = _listBag.Count;

        while (_listBag.TryTake(out _)) { }

        _ = System.Threading.Interlocked.Add(ref _trimmed, count);
        TraceOccurred?.Invoke($"Dispose(): Removed all {count} List<{typeof(T).Name}> instances from pool");

        return count;
    }

    /// <summary>
    /// Gets statistics about the pool's usage.
    /// </summary>
    /// <returns>A dictionary containing statistics about the pool.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetStatistics()
    {
        return new System.Collections.Generic.Dictionary<System.String, System.Object>
        {
            ["MaxPoolSize"] = _maxPoolSize,
            ["InitialCapacity"] = _initialCapacity,
            ["AvailableCount"] = AvailableCount,
            ["CreatedCount"] = CreatedCount,
            ["RentedCount"] = RentedCount,
            ["TotalRentOperations"] = TotalRentOperations,
            ["TotalReturnOperations"] = TotalReturnOperations,
            ["TrimmedCount"] = TrimmedCount,
            ["UptimeMs"] = UptimeMs
        };
    }

    /// <summary>
    /// Resets the statistics of the pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        _ = System.Threading.Interlocked.Exchange(ref _rented, 0);
        _ = System.Threading.Interlocked.Exchange(ref _returned, 0);
        _ = System.Threading.Interlocked.Exchange(ref _created, 0);
        _ = System.Threading.Interlocked.Exchange(ref _trimmed, 0);
        _uptime.Restart();
    }

    #endregion Public Methods

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 ValidateCapacity(System.Int32 capacity) => capacity <= 0 ? DefaultInitialCapacity : capacity > MaxInitialCapacity ? MaxInitialCapacity : capacity;

    #endregion Private Methods
}
