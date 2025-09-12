// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Middleware;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Shared;

namespace Nalix.Network.Middleware;

/// <summary>
/// Represents a network buffer middleware pipeline that executes middleware
/// in order defined by <see cref="MiddlewareOrderAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Middleware are registered via <see cref="Use"/> and executed sequentially
/// when <see cref="ExecuteAsync"/> is invoked.
/// </para>
/// <para>
/// The execution order is determined solely by <see cref="MiddlewareOrderAttribute"/>.
/// Middleware with lower order values run earlier in the pipeline.
/// </para>
/// <para>
/// The pipeline is thread-safe for registration and execution. A snapshot of the
/// middleware list is created per execution to avoid holding locks during invocation.
/// </para>
/// <para>
/// Middleware are composed into a delegate chain (similar to ASP.NET Core),
/// where each component is responsible for invoking the next delegate.
/// </para>
/// </remarks>
public class NetworkBufferMiddlewarePipeline
{
    #region Fields

    private readonly System.Threading.Lock _lock = new();
    private readonly System.Collections.Generic.List<MiddlewareEntry> _middlewares = [];
    private readonly System.Collections.Generic.HashSet<INetworkBufferMiddleware> _registered = [];

    private volatile System.Boolean _isSorted;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Registers a middleware instance in the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware to register.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="middleware"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the middleware has already been registered.
    /// </exception>
    /// <remarks>
    /// The execution order is determined by <see cref="MiddlewareOrderAttribute"/>.
    /// If no attribute is defined, the default order is <c>0</c>.
    /// </remarks>
    public void Use(INetworkBufferMiddleware middleware)
    {
        System.ArgumentNullException.ThrowIfNull(middleware);

        lock (_lock)
        {
            if (!_registered.Add(middleware))
            {
                throw new System.InvalidOperationException(
                    $"Middleware '{middleware.GetType().FullName}' already registered.");
            }

            System.Int32 order = 0;
            if (System.Attribute.GetCustomAttribute(middleware.GetType(), typeof(MiddlewareOrderAttribute))
                is MiddlewareOrderAttribute orderAttr)
            {
                order = orderAttr.Order;
            }
            _middlewares.Add(new MiddlewareEntry(middleware, order));
            _isSorted = false;
        }
    }

    /// <summary>
    /// Removes all middleware from the pipeline.
    /// </summary>
    /// <remarks>
    /// This operation is thread-safe and resets the pipeline to an empty state.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            _middlewares.Clear();
            _registered.Clear();
            _isSorted = false;
        }
    }

    /// <summary>
    /// Executes the middleware pipeline for the specified buffer and connection.
    /// </summary>
    /// <param name="buffer">The input buffer to process.</param>
    /// <param name="connection">The connection associated with the buffer.</param>
    /// <param name="ct">A token used to observe cancellation requests.</param>
    /// <returns>
    /// A task that resolves to the processed <see cref="IBufferLease"/>, or
    /// <see langword="null"/> if processing is short-circuited.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A snapshot of the registered middleware is taken at the start of execution,
    /// ensuring consistent behavior without holding locks during invocation.
    /// </para>
    /// <para>
    /// Middleware are executed in order, each receiving a delegate to invoke the next
    /// component in the pipeline.
    /// </para>
    /// <para>
    /// If a middleware does not invoke the next delegate or returns <see langword="null"/>,
    /// the pipeline execution is terminated early.
    /// </para>
    /// </remarks>
    public System.Threading.Tasks.Task<IBufferLease> ExecuteAsync(
        IBufferLease buffer,
        IConnection connection,
        System.Threading.CancellationToken ct = default)
    {
        System.Collections.Generic.List<MiddlewareEntry> snapshot;

        lock (_lock)
        {
            ENSURE_SORTED();
            snapshot = [.. _middlewares];
        }

        System.Func<IBufferLease, System.Threading.CancellationToken,
            System.Threading.Tasks.Task<IBufferLease>> next = (buf, _) => System.Threading.Tasks.Task.FromResult<IBufferLease>(buf);

        for (System.Int32 i = snapshot.Count - 1; i >= 0; i--)
        {
            MiddlewareEntry current = snapshot[i];
            System.Func<IBufferLease, System.Threading.CancellationToken, System.Threading.Tasks.Task<IBufferLease>> localNext = next;
            next = async (buffer, token) => await current.Middleware.InvokeAsync(buffer, connection, token, localNext);
        }

        return next(buffer, ct);
    }

    #endregion Public Methods

    #region Private Methods

    private void ENSURE_SORTED()
    {
        if (_isSorted)
        {
            return;
        }

        _middlewares.Sort((a, b) => a.Order.CompareTo(b.Order));
        _isSorted = true;
    }

    private record MiddlewareEntry(INetworkBufferMiddleware Middleware, System.Int32 Order);

    #endregion Private Methods
}