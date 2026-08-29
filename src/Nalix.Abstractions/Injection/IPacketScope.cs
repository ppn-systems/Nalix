// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking;

namespace Nalix.Abstractions.Injection;

/// <summary>
/// Defines a scope for managing the lifecycle of per-packet (scoped) dependencies.
/// </summary>
public interface IPacketScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the connection that owns the packet currently being processed in this scope.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the scope has not been attached to a connection yet.</exception>
    IConnection Connection { get; }

    /// <summary>
    /// Gets a required service of type <typeparamref name="T"/> from the current packet scope.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve.</typeparam>
    /// <returns>The resolved service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service is not registered in the scope.</exception>
    T GetRequiredService<T>() where T : class;

    /// <summary>
    /// Gets or creates a value cached for the lifetime of this packet scope, keyed by <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of value to cache.</typeparam>
    /// <param name="factory">Invoked at most once per scope to create the value.</param>
    /// <returns>The cached (or newly created) value.</returns>
    T GetOrAdd<T>(Func<T> factory) where T : class;

    /// <summary>
    /// Gets or creates a value cached for the lifetime of this packet scope, keyed by <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="T">The type of value to cache.</typeparam>
    /// <param name="key">The cache key, allowing multiple cached values of the same type.</param>
    /// <param name="factory">Invoked at most once per scope/key pair to create the value.</param>
    /// <returns>The cached (or newly created) value.</returns>
    T GetOrAdd<T>(object key, Func<T> factory) where T : class;

    /// <summary>
    /// Gets an optional service of type <typeparamref name="T"/> from the current packet scope.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve.</typeparam>
    /// <returns>The resolved service instance, or <see langword="null"/> if not registered.</returns>
    T? GetService<T>() where T : class;

    /// <summary>
    /// Registers an <see cref="IDisposable"/> instance to be disposed when this scope completes.
    /// </summary>
    /// <param name="disposable">The disposable instance to register.</param>
    void RegisterForDisposal(IDisposable disposable);

    /// <summary>
    /// Registers an <see cref="IAsyncDisposable"/> instance to be disposed asynchronously when this scope completes.
    /// </summary>
    /// <param name="asyncDisposable">The async disposable instance to register.</param>
    void RegisterForDisposal(IAsyncDisposable asyncDisposable);
}
