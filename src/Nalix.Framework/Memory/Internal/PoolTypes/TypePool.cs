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
    private int _count;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the number of objects available in this pool.
    /// </summary>
    public int AvailableCount => Volatile.Read(ref _count);

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
        int max = Volatile.Read(ref _maxCapacity);
        int current = Volatile.Read(ref _count);
        if (current >= max)
        {
            return false;
        }

        if (Interlocked.Increment(ref _count) <= max)
        {
            _objects.Push(obj);
            return true;
        }

        _ = Interlocked.Decrement(ref _count);
        return false;
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
    /// <returns>The number of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Clear()
    {
        int count = _objects.Count;
        _objects.Clear();
        Volatile.Write(ref _count, 0);
        return count;
    }

    /// <summary>
    /// Trims the pool to a target size based on a percentage of the maximum capacity.
    /// </summary>
    /// <param name="percentage">
    /// The percentage of the maximum capacity to keep (0-100).
    /// <list type="bullet">
    ///   <item><description><c>0</c> = no trim (pool is at or below its safety floor).</description></item>
    ///   <item><description><c>1–99</c> = keep <c>percentage</c>% of <see cref="MaxCapacity"/>.</description></item>
    ///   <item><description><c>100</c> = keep up to full <see cref="MaxCapacity"/>.</description></item>
    ///   <item><description><c>&lt; 0</c> = clear all objects.</description></item>
    /// </list>
    /// </param>
    /// <param name="decayFactor">Fraction of excess objects to remove (0.0-1.0). Default is 1.0 (remove all excess).</param>
    /// <returns>The number of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Trim(int percentage, double decayFactor = 1.0)
    {
        if (percentage < 0)
        {
            return this.Clear();
        }

        if (percentage == 0)
        {
            return 0; // Safety floor reached — retain all available objects.
        }

        int targetSize = percentage >= 100
            ? _maxCapacity
            : _maxCapacity * percentage / 100;

        int currentCount = _objects.Count;
        if (currentCount <= targetSize)
        {
            return 0;
        }

        int excess = currentCount - targetSize;
        int toRemove = decayFactor >= 1.0
            ? excess
            : System.Math.Max(1, (int)(excess * decayFactor));

        int removed = RemoveObjects(_objects, toRemove);
        Volatile.Write(ref _count, _objects.Count);
        return removed;

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

