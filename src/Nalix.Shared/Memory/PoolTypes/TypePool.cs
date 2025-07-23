using Nalix.Common.Caching;
using Nalix.Shared.Memory.Pools;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Memory.PoolTypes;

/// <summary>
/// Type-specific object pool implementation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TypePool"/> class.
/// </remarks>
/// <param name="maxCapacity">The maximum capacity of this pool.</param>
internal class TypePool(System.Int32 maxCapacity)
{
    #region Fields

    private readonly ConcurrentStack<IPoolable> _objects = new();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the ProtocolType of objects available in this pool.
    /// </summary>
    public System.Int32 AvailableCount => _objects.Count;

    /// <summary>
    /// Gets the maximum capacity of this pool.
    /// </summary>
    public System.Int32 MaxCapacity { get; private set; } = maxCapacity > 0 ? maxCapacity : ObjectPool.DefaultMaxSize;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Sets the maximum capacity of this pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity of this pool.</param>
    public void SetMaxCapacity(System.Int32 maxCapacity)
    {
        if (maxCapacity < 0)
        {
            return;
        }

        System.Int32 oldCapacity = MaxCapacity;
        MaxCapacity = maxCapacity;

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
    public System.Boolean TryPush(IPoolable obj)
    {
        if (_objects.Count >= MaxCapacity)
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
    public System.Boolean TryPop(out IPoolable? obj) => _objects.TryPop(out obj);

    /// <summary>
    /// Clears all objects from this pool.
    /// </summary>
    /// <returns>The ProtocolType of objects removed.</returns>
    public System.Int32 Clear()
    {
        System.Int32 count = _objects.Count;
        _objects.Clear();
        return count;
    }

    /// <summary>
    /// Trims the pool to a target size based on a percentage of the maximum capacity.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <returns>The ProtocolType of objects removed.</returns>
    public System.Int32 Trim(System.Int32 percentage)
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
        System.Int32 targetSize = MaxCapacity * percentage / 100;
        System.Int32 currentCount = _objects.Count;

        if (currentCount <= targetSize)
        {
            // No need to trim
            return 0;
        }

        // Remove objects until we reach the target size
        System.Int32 toRemove = currentCount - targetSize;
        System.Int32 removed = 0;

        for (System.Int32 i = 0; i < toRemove; i++)
        {
            if (_objects.TryPop(out _))
            {
                removed++;
            }
            else
            {
                // No more objects to remove
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
    public IPoolable[] ToArray() => [.. _objects];

    #endregion Public Methods
}
