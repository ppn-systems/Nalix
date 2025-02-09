using System;
using System.Collections.Generic;

namespace Notio.Serialization.Internal.Reflection;

/// <summary>
/// A thread-safe cache of members belonging to a given type.
///
/// The Retrieve method is the most useful one in this class as it
/// calls the retrieval process if the type is not contained
/// in the cache.
/// </summary>
/// <typeparam name="T">The type of Member to be cached.</typeparam>
internal abstract class TypeCache<T> : ReflectionCache<T>
{
    /// <summary>
    /// Retrieves the properties stored for the specified type.
    /// If the properties are not available, it calls the factory method to retrieve them
    /// and returns them as an array of PropertyInfo.
    /// </summary>
    /// <typeparam name="TOut">The type of the out.</typeparam>
    /// <param name="factory">The factory.</param>
    /// <returns>An array of the properties stored for the specified type.</returns>
    public IEnumerable<T> Retrieve<TOut>(Func<Type, IEnumerable<T>> factory)
        => Retrieve(typeof(TOut), factory);
}