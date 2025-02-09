using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Notio.Shared.Injection;

/// <summary>
/// Manages instances of different types, ensuring that each type is only created once.
/// </summary>
public sealed class InstanceManager
{
    private readonly ConcurrentDictionary<Type, object> _instanceCache = new();

    /// <summary>
    /// Gets or creates an instance of the specified type.
    /// </summary>
    /// <param name="type">The type of the instance to get or create.</param>
    /// <param name="args">The arguments to pass to the constructor if a new instance is created.</param>
    /// <returns>The existing or newly created instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified type does not have a public constructor.</exception>
    public object GetOrCreateInstance(Type type, params object[] args)
    {
        return _instanceCache.GetOrAdd(type, t =>
        {
            var constructor = t.GetConstructors().FirstOrDefault();
            return constructor == null
                ? throw new InvalidOperationException($"Type {t.Name} does not have a public constructor.")
                : constructor.Invoke(args);
        });
    }

    /// <summary>
    /// Removes the instance of the specified type from the cache.
    /// </summary>
    /// <param name="type">The type of the instance to remove.</param>
    /// <returns><c>true</c> if the instance was successfully removed; otherwise, <c>false</c>.</returns>
    public bool RemoveInstance(Type type)
        => _instanceCache.TryRemove(type, out _);
}