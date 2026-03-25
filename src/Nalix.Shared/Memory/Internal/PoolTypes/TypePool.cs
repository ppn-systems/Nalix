// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal.PoolTypes;

/// <summary>
/// Type-specific object pool implementation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TypePool"/> class.
/// </remarks>
/// <param name="maxCapacity">The maximum capacity of this pool.</param>
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal class TypePool(int maxCapacity)
{
    #region Fields

    private int _count;
    private int _maxCapacity = maxCapacity;
    private readonly System.Collections.Concurrent.ConcurrentStack<IPoolable> _objects = new();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the ProtocolType of objects available in this pool.
    /// </summary>
    public int AvailableCount => _objects.Count;

    /// <summary>
    /// Gets the maximum capacity of this pool.
    /// </summary>
    public int MaxCapacity => System.Threading.Volatile.Read(ref _maxCapacity);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Sets the maximum capacity of this pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity of this pool.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetMaxCapacity(int maxCapacity)
    {
        if (maxCapacity < 0)
        {
            return;
        }

        int oldCapacity = System.Threading.Interlocked.Exchange(ref _maxCapacity, maxCapacity);

        // If the new capacity is less than the old one, trim the pool
        if (maxCapacity < oldCapacity)
        {
            _ = Trim(100); // Trim to exactly the max capacity
        }
    }

    /// <summary>
    /// Tries to add an object to the pool.
    /// </summary>
    /// <param name="obj">The object to add.</param>
    /// <returns>True if the object was added, false if the pool is full.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryPush(IPoolable obj)
    {
        int newCount = System.Threading.Interlocked.Increment(ref _count);

        if (newCount > _maxCapacity)
        {
            _ = System.Threading.Interlocked.Decrement(ref _count);
            return false;
        }

        _objects.Push(obj);
        return true;
    }

    /// <summary>
    /// Tries to get an object from the pool.
    /// </summary>
    /// <param name="obj">The object from the pool.</param>
    /// <returns>True if an object was retrieved, false if the pool is empty.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out IPoolable? obj)
    {
        if (_objects.TryPop(out obj))
        {
            _ = System.Threading.Interlocked.Decrement(ref _count);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears all objects from this pool.
    /// </summary>
    /// <returns>The ProtocolType of objects removed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int Clear()
    {
        int count = _objects.Count;
        _objects.Clear();
        _ = System.Threading.Interlocked.Exchange(ref _count, 0);
        return count;
    }

    /// <summary>
    /// Trims the pool to a target size based on a percentage of the maximum capacity.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <returns>The ProtocolType of objects removed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public int Trim(int percentage)
    {
        if (percentage >= 100)
        {
            // Keep everything up to max capacity
            return 0;
        }

        if (percentage <= 0)
        {
            // Dispose everything
            return Clear();
        }

        // Calculate the target size
        int targetSize = MaxCapacity * percentage / 100;
        int currentCount = _objects.Count;

        if (currentCount <= targetSize)
        {
            // No need to trim
            return 0;
        }

        // Remove objects until we reach the target size
        int toRemove = currentCount - targetSize;
        int removed = 0;

        for (int i = 0; i < toRemove; i++)
        {
            if (_objects.TryPop(out _))
            {
                _ = System.Threading.Interlocked.Decrement(ref _count);
                removed++;
            }
            else
            {
                break;
            }
        }

        return removed;
    }

    /// <summary>
    /// Gets the objects in this pool as an array.
    /// </summary>
    /// <remarks>This is primarily for diagnostic purposes and should not be used in performance-critical code.</remarks>
    /// <returns>An array containing the objects in this pool.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IPoolable[] ToArray() => [.. _objects];

    #endregion Public Methods
}
