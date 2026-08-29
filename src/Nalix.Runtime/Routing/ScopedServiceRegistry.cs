// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using Nalix.Abstractions.Injection;

namespace Nalix.Runtime.Routing;

/// <summary>
/// Thread-safe registry that maps service types to their per-packet factory delegates.
/// </summary>
public sealed class ScopedServiceRegistry
{
    private static readonly Lazy<ScopedServiceRegistry> s_instance = new(() => new ScopedServiceRegistry(), isThreadSafe: true);

    /// <summary>
    /// Gets the shared global registry instance.
    /// </summary>
    public static ScopedServiceRegistry Instance => s_instance.Value;

    private readonly ConcurrentDictionary<Type, Func<IPacketScope, object>> _factories = new();

    /// <summary>
    /// When <see langword="true"/>, packet scopes resolved from this registry no longer fall back to
    /// <see cref="Nalix.Framework.Injection.InstanceManager"/> singletons for types with no registered
    /// scoped factory: <c>GetRequiredService</c> throws instead of silently returning a singleton.
    /// Defaults to <see langword="false"/> to preserve existing fallback behavior.
    /// </summary>
    public bool StrictScopes { get; set; }

    /// <summary>
    /// Registers a factory for a scoped service of type <typeparamref name="TService"/>.
    /// </summary>
    /// <typeparam name="TService">The service contract type.</typeparam>
    /// <param name="factory">The factory that creates an instance using the active packet scope.</param>
    public void RegisterScoped<TService>(Func<IPacketScope, TService> factory) where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories[typeof(TService)] = scope => factory(scope);
    }

    /// <summary>
    /// Registers a factory for a scoped service by explicit service type.
    /// </summary>
    /// <param name="serviceType">The service contract type.</param>
    /// <param name="factory">The factory that creates an instance using the active packet scope.</param>
    public void RegisterScoped(Type serviceType, Func<IPacketScope, object> factory)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[serviceType] = factory;
    }

    /// <summary>
    /// Attempts to retrieve a registered factory for the specified service type.
    /// </summary>
    /// <param name="serviceType">The service contract type.</param>
    /// <param name="factory">The factory if found; otherwise, null.</param>
    /// <returns>True if a factory is registered; otherwise, false.</returns>
    public bool TryGetFactory(Type serviceType, out Func<IPacketScope, object>? factory)
        => _factories.TryGetValue(serviceType, out factory);

    /// <summary>
    /// Clears all registered scoped factories.
    /// </summary>
    public void Clear() => _factories.Clear();
}
