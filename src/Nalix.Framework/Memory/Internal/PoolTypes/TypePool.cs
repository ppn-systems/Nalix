// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;


#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
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

    private int _maxCapacity = maxCapacity;
    private readonly ConcurrentStack<IPoolable> _objects = new();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the number of objects available in this pool.
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
        if (_objects.Count >= Volatile.Read(ref _maxCapacity))
        {
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
    public bool TryPop(out IPoolable? obj) => _objects.TryPop(out obj);

    /// <summary>
    /// Clears all objects from this pool.
    /// </summary>
    /// <returns>The number of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Clear()
    {
        int count = _objects.Count;
        _objects.Clear();
        return count;
    }

    /// <summary>
    /// Trims the pool to a target size based on a percentage of the maximum capacity.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <returns>The number of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Trim(int percentage)
    {
        if (percentage <= 0)
        {
            return this.Clear();
        }

        int targetSize = percentage >= 100
            ? _maxCapacity
            : _maxCapacity * percentage / 100;

        int currentCount = _objects.Count;
        if (currentCount <= targetSize)
        {
            return 0;
        }

        int toRemove = currentCount - targetSize;

        return RemoveObjects(_objects, toRemove);

        static int RemoveObjects(ConcurrentStack<IPoolable> stack, int count)
        {
            int removed = 0;

            for (int i = 0; i < count; i++)
            {
                if (stack.TryPop(out _))
                {
                    removed++;
                }
                else
                {
                    break;
                }
            }

            return removed;
        }
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

