// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Injection;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Injection;
using Nalix.Runtime.Routing;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// High-performance, zero-allocation packet scope that manages per-packet service resolutions and disposals.
/// </summary>
public sealed class PacketScope : IPacketScope, IPoolable, IDisposable, IAsyncDisposable
{
    private const int InitialCapacity = 16;
    private const int InitialCacheCapacity = 8;
    private const int InitialDisposableCapacity = 8;

    private readonly ScopedServiceRegistry _registry;

    private int _cacheCount;
    private int _resolvedCount;
    private int _disposablesCount;

    private object[] _disposables = new object[InitialDisposableCapacity];
    private (Type Type, object Instance)[] _resolved = new (Type, object)[InitialCapacity];
    private (object Key, object Instance)[] _cache = new (object, object)[InitialCacheCapacity];

    private int _disposed;
    private IConnection? _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketScope"/> class.
    /// </summary>
    public PacketScope() : this(ScopedServiceRegistry.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketScope"/> class with an explicit registry.
    /// </summary>
    /// <param name="registry">The scoped service registry to resolve factories from.</param>
    public PacketScope(ScopedServiceRegistry registry) => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <inheritdoc/>
    public IConnection Connection => _connection
        ?? throw new InvalidOperationException("This packet scope has not been attached to a connection yet.");

    /// <summary>
    /// Attaches the connection that owns the packet being processed in this scope.
    /// </summary>
    /// <param name="connection">The connection to attach.</param>
    internal void AttachConnection(IConnection connection) => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public T GetRequiredService<T>() where T : class
    {
        T? service = this.GetService<T>();
        if (service is not null)
        {
            return service;
        }

        throw new InvalidOperationException(
            $"No service for type '{typeof(T).FullName}' has been registered in the active packet scope or singleton container.");
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public T GetOrAdd<T>(Func<T> factory) where T : class => this.GetOrAdd(typeof(T), factory);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public T GetOrAdd<T>(object key, Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        for (int i = 0; i < _cacheCount; i++)
        {
            if (Equals(_cache[i].Key, key))
            {
                return (T)_cache[i].Instance;
            }
        }

        T instance = factory();

        if (instance is IAsyncDisposable asyncDisp)
        {
            this.RegisterForDisposal(asyncDisp);
        }
        else if (instance is IDisposable syncDisp)
        {
            this.RegisterForDisposal(syncDisp);
        }

        if (_cacheCount >= _cache.Length)
        {
            Array.Resize(ref _cache, _cache.Length * 2);
        }

        _cache[_cacheCount++] = (key, instance);
        return instance;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public T? GetService<T>() where T : class
    {
        Type targetType = typeof(T);

        // 1. Fast linear scan over already resolved scoped instances (Zero Allocation)
        for (int i = 0; i < _resolvedCount; i++)
        {
            if (_resolved[i].Type == targetType)
            {
                return (T)_resolved[i].Instance;
            }
        }

        // 2. Try resolving from ScopedServiceRegistry
        if (_registry.TryGetFactory(targetType, out Func<IPacketScope, object>? factory) && factory is not null)
        {
            object instance = factory(this);

            if (instance is IAsyncDisposable asyncDisp)
            {
                this.RegisterForDisposal(asyncDisp);
            }
            else if (instance is IDisposable syncDisp)
            {
                this.RegisterForDisposal(syncDisp);
            }

            this.AddResolved(targetType, instance);
            return (T)instance;
        }

        // 3. Fallback: resolve from Singleton InstanceManager (skipped when the registry is strict)
        if (_registry.StrictScopes)
        {
            return null;
        }

        T? singleton = InstanceManager.Instance.GetExistingInstance<T>();
        if (singleton is not null)
        {
            return singleton;
        }

        return null;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RegisterForDisposal(IDisposable disposable)
    {
        if (disposable is null)
        {
            return;
        }

        if (_disposablesCount >= _disposables.Length)
        {
            Array.Resize(ref _disposables, _disposables.Length * 2);
        }

        _disposables[_disposablesCount++] = disposable;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RegisterForDisposal(IAsyncDisposable asyncDisposable)
    {
        if (asyncDisposable is null)
        {
            return;
        }

        if (_disposablesCount >= _disposables.Length)
        {
            Array.Resize(ref _disposables, _disposables.Length * 2);
        }

        _disposables[_disposablesCount++] = asyncDisposable;
    }

    private void AddResolved(Type type, object instance)
    {
        if (_resolvedCount >= _resolved.Length)
        {
            Array.Resize(ref _resolved, _resolved.Length * 2);
        }

        _resolved[_resolvedCount++] = (type, instance);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // 1. Unwind single disposables stack in strict LIFO order
        for (int i = _disposablesCount - 1; i >= 0; i--)
        {
            object item = _disposables[i];
            _disposables[i] = null!;

            try
            {
                if (item is IAsyncDisposable asyncDisp)
                {
                    await asyncDisp.DisposeAsync().ConfigureAwait(false);
                }
                else if (item is IDisposable syncDisp)
                {
                    syncDisp.Dispose();
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // Preserve execution integrity
            }
        }
        _disposablesCount = 0;

        // 2. Clear resolved references
        for (int i = 0; i < _resolvedCount; i++)
        {
            _resolved[i] = default;
        }
        _resolvedCount = 0;

        // 3. Clear GetOrAdd cache
        for (int i = 0; i < _cacheCount; i++)
        {
            _cache[i] = default;
        }
        _cacheCount = 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // 1. Unwind single disposables stack in strict LIFO order
        for (int i = _disposablesCount - 1; i >= 0; i--)
        {
            object item = _disposables[i];
            _disposables[i] = null!;

            try
            {
                if (item is IDisposable syncDisposable)
                {
                    syncDisposable.Dispose();
                }
                else if (item is IAsyncDisposable asyncDisp)
                {
                    asyncDisp.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
            }
        }
        _disposablesCount = 0;

        // 2. Clear resolved references
        for (int i = 0; i < _resolvedCount; i++)
        {
            _resolved[i] = default;
        }
        _resolvedCount = 0;

        // 3. Clear GetOrAdd cache
        for (int i = 0; i < _cacheCount; i++)
        {
            _cache[i] = default;
        }
        _cacheCount = 0;
    }

    /// <inheritdoc/>
    public void ResetForPool()
    {
        this.Dispose();
        Volatile.Write(ref _disposed, 0);
        _connection = null;

        // Shrink arrays that grew beyond the pooled baseline so a scope that once
        // resolved many services doesn't hold that capacity forever in the pool.
        if (_resolved.Length > InitialCapacity)
        {
            _resolved = new (Type, object)[InitialCapacity];
        }

        if (_disposables.Length > InitialDisposableCapacity)
        {
            _disposables = new object[InitialDisposableCapacity];
        }

        if (_cache.Length > InitialCacheCapacity)
        {
            _cache = new (object, object)[InitialCacheCapacity];
        }
    }
}
