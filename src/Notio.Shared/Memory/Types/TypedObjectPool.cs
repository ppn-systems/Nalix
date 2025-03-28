using Notio.Common.Caching;
using Notio.Shared.Memory.Pools;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Notio.Shared.Memory.Types;

/// <summary>
/// A type-specific adapter for the object pool that eliminates the need for runtime type checking.
/// </summary>
/// <typeparam name="T">The type of objects managed by this pool.</typeparam>
public sealed class TypedObjectPool<T> where T : IPoolable, new()
{
    private readonly ObjectPool _parentPool;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedObjectPool{T}"/> class.
    /// </summary>
    /// <param name="parentPool">The parent object pool.</param>
    internal TypedObjectPool(ObjectPool parentPool)
    {
        _parentPool = parentPool;
    }

    /// <summary>
    /// Gets an object from the pool.
    /// </summary>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get()
    {
        return _parentPool.Get<T>();
    }

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="obj">The object to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T obj)
    {
        _parentPool.Return(obj);
    }

    /// <summary>
    /// Gets multiple objects from the pool.
    /// </summary>
    /// <param name="count">The number of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    public List<T> GetMultiple(int count)
    {
        return _parentPool.GetMultiple<T>(count);
    }

    /// <summary>
    /// Returns multiple objects to the pool.
    /// </summary>
    /// <param name="objects">The objects to return.</param>
    /// <returns>The number of objects successfully returned to the pool.</returns>
    public int ReturnMultiple(IEnumerable<T> objects)
    {
        return _parentPool.ReturnMultiple(objects);
    }

    /// <summary>
    /// Sets the maximum capacity for this type's pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity.</param>
    public void SetMaxCapacity(int maxCapacity)
    {
        _parentPool.SetMaxCapacity<T>(maxCapacity);
    }

    /// <summary>
    /// Preallocates objects in the pool.
    /// </summary>
    /// <param name="count">The number of objects to preallocate.</param>
    /// <returns>The number of objects successfully preallocated.</returns>
    public int Prealloc(int count)
    {
        return _parentPool.Prealloc<T>(count);
    }

    /// <summary>
    /// Gets information about this type's pool.
    /// </summary>
    /// <returns>A dictionary containing pool statistics for this type.</returns>
    public Dictionary<string, object> GetInfo()
    {
        return _parentPool.GetTypeInfo<T>();
    }

    /// <summary>
    /// Clears this type's pool.
    /// </summary>
    /// <returns>The number of objects removed.</returns>
    public int Clear()
    {
        return _parentPool.ClearType<T>();
    }
}
