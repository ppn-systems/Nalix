using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Notio.Common.Injection;

public sealed class InstanceManager
{
    private readonly ConcurrentDictionary<Type, object> _instanceCache = new();

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

    public bool RemoveInstance(Type type)
        => _instanceCache.TryRemove(type, out _);
}