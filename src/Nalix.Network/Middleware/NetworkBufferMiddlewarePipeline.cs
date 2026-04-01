// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;

namespace Nalix.Network.Middleware;

/// <summary>
/// Executes network buffer middleware in a deterministic, low-allocation sequence.
/// </summary>
/// <remarks>
/// <para>
/// Ownership rules:
/// </para>
/// <list type="bullet">
/// <item><description>The pipeline owns the currently active lease while executing.</description></item>
/// <item><description>When middleware returns a replacement lease, the previous lease is disposed immediately.</description></item>
/// <item><description>When middleware returns <see langword="null"/>, the active lease is disposed and execution stops.</description></item>
/// </list>
/// </remarks>
public sealed class NetworkBufferMiddlewarePipeline
{
    #region Fields

    private readonly Lock _lock = new();
    private readonly List<MiddlewareEntry> _entries = [];
    private readonly HashSet<INetworkBufferMiddleware> _registered = [];

    private volatile bool _isSorted;
    private INetworkBufferMiddleware[] _snapshot = [];

    #endregion Fields

    #region APIs

    /// <summary>
    /// Registers a middleware instance.
    /// </summary>
    /// <param name="middleware">Middleware instance.</param>
    public void Use(INetworkBufferMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        lock (_lock)
        {
            if (!_registered.Add(middleware))
            {
                throw new InternalErrorException(
                    $"Middleware '{middleware.GetType().FullName}' already registered.");
            }

            int order = 0;
            if (Attribute.GetCustomAttribute(middleware.GetType(), typeof(MiddlewareOrderAttribute))
                is MiddlewareOrderAttribute orderAttribute)
            {
                order = orderAttribute.Order;
            }

            _entries.Add(new MiddlewareEntry(middleware, order));
            _isSorted = false;
            this.REBUILD_SNAPSHOT_UNSAFE();
        }
    }

    /// <summary>
    /// Clears all middleware registrations.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _registered.Clear();
            _isSorted = true;
            Volatile.Write(ref _snapshot, []);
        }
    }

    /// <summary>
    /// Executes middleware over the provided lease.
    /// </summary>
    /// <param name="buffer">Incoming lease.</param>
    /// <param name="connection">Owning connection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transformed lease, or <see langword="null"/> if dropped.</returns>
    public ValueTask<IBufferLease?> ExecuteAsync(IBufferLease buffer, IConnection connection, CancellationToken ct = default)
    {
        if (buffer is null || connection is null)
        {
            buffer?.Dispose();
            return ValueTask.FromResult<IBufferLease?>(null);
        }

        INetworkBufferMiddleware[] middleware = Volatile.Read(ref _snapshot);
        if (middleware.Length == 0)
        {
            return ValueTask.FromResult<IBufferLease?>(buffer);
        }

        return ExecuteAsync(middleware, buffer, connection, ct);
    }

    #endregion APIs

    #region Private Methods

    private static ValueTask<IBufferLease?> ExecuteAsync(INetworkBufferMiddleware[] middleware, IBufferLease current, IConnection connection, CancellationToken token)
    {
        try
        {
            for (int i = 0; i < middleware.Length; i++)
            {
                ValueTask<IBufferLease?> pending = middleware[i].InvokeAsync(current, connection, token);
                if (!pending.IsCompletedSuccessfully)
                {
                    return ExecuteAsync(middleware, i, current, connection, token, pending);
                }

                IBufferLease? next = pending.Result;
                if (next is null)
                {
                    current.Dispose();
                    return ValueTask.FromResult<IBufferLease?>(null);
                }

                if (!ReferenceEquals(next, current))
                {
                    current.Dispose();
                    current = next;
                }
            }

            return ValueTask.FromResult<IBufferLease?>(current);
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private static async ValueTask<IBufferLease?> ExecuteAsync(
        INetworkBufferMiddleware[] middleware,
        int index,
        IBufferLease current,
        IConnection connection,
        CancellationToken token,
        ValueTask<IBufferLease?> firstPending)
    {
        try
        {
            IBufferLease? next = await firstPending.ConfigureAwait(false);
            if (next is null)
            {
                current.Dispose();
                return null;
            }

            if (!ReferenceEquals(next, current))
            {
                current.Dispose();
                current = next;
            }

            for (int i = index + 1; i < middleware.Length; i++)
            {
                next = await middleware[i].InvokeAsync(current, connection, token).ConfigureAwait(false);
                if (next is null)
                {
                    current.Dispose();
                    return null;
                }

                if (!ReferenceEquals(next, current))
                {
                    current.Dispose();
                    current = next;
                }
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private void REBUILD_SNAPSHOT_UNSAFE()
    {
        this.ENSURE_SORTED_UNSAFE();

        INetworkBufferMiddleware[] arr = new INetworkBufferMiddleware[_entries.Count];
        for (int i = 0; i < _entries.Count; i++)
        {
            arr[i] = _entries[i].Middleware;
        }

        Volatile.Write(ref _snapshot, arr);
    }

    private void ENSURE_SORTED_UNSAFE()
    {
        if (_isSorted)
        {
            return;
        }

        _entries.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        _isSorted = true;
    }

    private readonly record struct MiddlewareEntry(INetworkBufferMiddleware Middleware, int Order);

    #endregion Private Methods
}
