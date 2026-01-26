// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Abstractions;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Framework.Memory.Internal.PoolTypes;

/// <summary>
/// Type-specific object pool implementation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TypePool"/> class.
/// </remarks>
/// <param name="maxCapacity">The maximum capacity of this pool.</param>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
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
    public int MaxCapacity => Volatile.Read(ref _maxCapacity);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Sets the maximum capacity of this pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity of this pool.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMaxCapacity(int maxCapacity)
    {
        if (maxCapacity < 0)
        {
            return;
        }

        int oldCapacity = Interlocked.Exchange(ref _maxCapacity, maxCapacity);

        // If the new capacity is less than the old one, trim the pool
        if (maxCapacity < oldCapacity)
        {
            _ = this.Trim(100); // Trim to exactly the max capacity
        }
    }

    /// <summary>
    /// Tries to add an object to the pool.
    /// </summary>
    /// <param name="obj">The object to add.</param>
    /// <returns>True if the object was added, false if the pool is full.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPush(IPoolable obj)
    {
        int newCount = Interlocked.Increment(ref _count);

        if (newCount > _maxCapacity)
        {
            _ = Interlocked.Decrement(ref _count);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out IPoolable? obj)
    {
        if (_objects.TryPop(out obj))
        {
            _ = Interlocked.Decrement(ref _count);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears all objects from this pool.
    /// </summary>
    /// <returns>The ProtocolType of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Clear()
    {
        int count = _objects.Count;
        _objects.Clear();
        _ = Interlocked.Exchange(ref _count, 0);
        return count;
    }

    /// <summary>
    /// Trims the pool to a target size based on a percentage of the maximum capacity.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <returns>The ProtocolType of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
            return this.Clear();
        }

        // Calculate the target size
        int targetSize = this.MaxCapacity * percentage / 100;
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
                _ = Interlocked.Decrement(ref _count);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPoolable[] ToArray() => [.. _objects];

    #endregion Public Methods
}
