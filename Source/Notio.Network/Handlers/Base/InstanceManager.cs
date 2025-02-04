using System;
using System.Collections.Concurrent;

namespace Notio.Network.Handlers.Base;

internal class InstanceManager
{
    private readonly ConcurrentDictionary<Type, object> _instanceCache = new();

    public object GetOrCreateInstance(Type type)
        => _instanceCache.GetOrAdd(type, t => Activator.CreateInstance(t)!);
}