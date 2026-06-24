// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using Nalix.Abstractions.Networking.Rpc;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for creating RPC clients over a transport session.
/// </summary>
public static class RpcExtensions
{
    private static readonly ConcurrentDictionary<Type, Func<TransportSession, object>> s_factories = new();

    /// <summary>
    /// Registers a factory delegate for a specific RPC service proxy. Used by Source Generators.
    /// </summary>
    public static void RegisterFactory(Type serviceType, Func<TransportSession, object> factory) => s_factories[serviceType] = factory;

    /// <summary>
    /// Creates a Typed-RPC client proxy for the specified service interface.
    /// The interface must be decorated with <see cref="RpcServiceAttribute"/>.
    /// </summary>
    /// <typeparam name="T">The RPC service interface type.</typeparam>
    /// <param name="session">The transport session.</param>
    /// <returns>A proxy instance implementing the requested service interface.</returns>
    public static T CreateRpcClient<T>(this TransportSession session) where T : class
    {
        ArgumentNullException.ThrowIfNull(session);

        Type serviceType = typeof(T);

        if (s_factories.TryGetValue(serviceType, out Func<TransportSession, object>? factory))
        {
            return (T)factory(session);
        }

        throw new InvalidOperationException($"No RPC client factory registered for service type '{serviceType.Name}'. Ensure the interface is marked with [RpcService] and the Source Generator is running.");
    }
}
