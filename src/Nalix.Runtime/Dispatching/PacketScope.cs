// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Injection;
using Nalix.Framework.Injection;
using Nalix.Runtime.Routing;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// High-performance, zero-allocation packet scope that manages per-packet service resolutions and disposals.
/// </summary>
public sealed class PacketScope : IPacketScope, IPoolable, IDisposable, IAsyncDisposable
{
    private const int InitialCapacity = 16;
    private const int InitialDisposableCapacity = 8;

    private readonly ScopedServiceRegistry _registry;

    private (Type Type, object Instance)[] _resolved = new (Type, object)[InitialCapacity];
    private int _resolvedCount;

    private IDisposable[] _disposables = new IDisposable[InitialDisposableCapacity];
    private int _disposablesCount;

    private IAsyncDisposable[] _asyncDisposables = new IAsyncDisposable[InitialDisposableCapacity];
    private int _asyncDisposablesCount;

    private int _disposed;

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

        // 3. Fallback: resolve from Singleton InstanceManager
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

        if (_asyncDisposablesCount >= _asyncDisposables.Length)
        {
            Array.Resize(ref _asyncDisposables, _asyncDisposables.Length * 2);
        }

        _asyncDisposables[_asyncDisposablesCount++] = asyncDisposable;
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

        // 1. Dispose async disposables in LIFO order
        for (int i = _asyncDisposablesCount - 1; i >= 0; i--)
        {
            try
            {
                await _asyncDisposables[i].DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // Preserve execution integrity
            }
            finally
            {
                _asyncDisposables[i] = null!;
            }
        }
        _asyncDisposablesCount = 0;

        // 2. Dispose synchronous disposables in LIFO order
        for (int i = _disposablesCount - 1; i >= 0; i--)
        {
            try
            {
                _disposables[i].Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
            }
            finally
            {
                _disposables[i] = null!;
            }
        }
        _disposablesCount = 0;

        // 3. Clear resolved references
        for (int i = 0; i < _resolvedCount; i++)
        {
            _resolved[i] = default;
        }
        _resolvedCount = 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // 1. Dispose async disposables in LIFO order (prefer sync Dispose if implemented)
        for (int i = _asyncDisposablesCount - 1; i >= 0; i--)
        {
            try
            {
                if (_asyncDisposables[i] is IDisposable syncDisposable)
                {
                    syncDisposable.Dispose();
                }
                else
                {
                    _asyncDisposables[i].DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
            }
            finally
            {
                _asyncDisposables[i] = null!;
            }
        }
        _asyncDisposablesCount = 0;

        // 2. Dispose synchronous disposables in LIFO order
        for (int i = _disposablesCount - 1; i >= 0; i--)
        {
            try
            {
                _disposables[i].Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
            }
            finally
            {
                _disposables[i] = null!;
            }
        }
        _disposablesCount = 0;

        // 3. Clear resolved references
        for (int i = 0; i < _resolvedCount; i++)
        {
            _resolved[i] = default;
        }
        _resolvedCount = 0;
    }

    /// <inheritdoc/>
    public void ResetForPool()
    {
        this.Dispose();
        Volatile.Write(ref _disposed, 0);
    }
}
