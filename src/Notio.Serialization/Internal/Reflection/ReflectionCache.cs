using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Notio.Serialization.Internal.Reflection;

/// <summary>
/// A thread-safe collection cache repository for types.
/// </summary>
/// <typeparam name="TValue">The type of member to cache.</typeparam>
internal class ReflectionCache<TValue>
{
    private readonly Lazy<ConcurrentDictionary<Type, IEnumerable<TValue>>> _data =
        new(() => new ConcurrentDictionary<Type, IEnumerable<TValue>>(), true);

    /// <summary>
    /// Retrieves the properties stored for the specified type.
    /// If the properties are not available, it calls the factory method to retrieve them
    /// and returns them as an array of PropertyInfo.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="factory">The factory.</param>
    /// <returns>
    /// An array of the properties stored for the specified type.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// key
    /// or
    /// factory.
    /// </exception>
    internal IEnumerable<TValue> Retrieve(Type key, Func<Type, IEnumerable<TValue>> factory)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        return _data.Value.GetOrAdd(key, k => factory.Invoke(k).Where(item => item != null));
    }
}