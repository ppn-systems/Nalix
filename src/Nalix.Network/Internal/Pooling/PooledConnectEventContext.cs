// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Network.Internal.Pooling;

/// <summary>
/// Represents a reusable pooled context for invoking a connection event callback.
/// The wrapper exists so callback dispatch can reuse the same object instead of
/// allocating a fresh closure/context for every event.
/// </summary>
/// <remarks>
/// The sender, arguments, and callback delegate are copied into this object just
/// long enough for the ThreadPool work item to run, then the instance is returned
/// to the pool and reused by the next callback.
/// </remarks>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PooledConnectEventContext : IPoolable
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    /// <summary>
    /// The event sender captured for this callback invocation.
    /// </summary>
    public object? Sender;

    /// <summary>
    /// The event arguments captured for this callback invocation.
    /// </summary>
    public IConnectEventArgs Args = default!;

    /// <summary>
    /// The callback delegate that will be invoked by the dispatcher.
    /// </summary>
    public EventHandler<IConnectEventArgs>? Callback;

    /// <summary>
    /// Initializes the pooled wrapper with the callback, sender, and arguments
    /// so they can be passed to the worker thread without allocating a closure.
    /// </summary>
    /// <param name="callback">The event handler to invoke.</param>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The event arguments.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize(EventHandler<IConnectEventArgs> callback, object? sender, IConnectEventArgs args)
    {
        Args = args;
        Sender = sender;
        Callback = callback;
    }

    /// <inheritdoc/>
    public static PooledConnectEventContext Get() => s_pool.Get<PooledConnectEventContext>();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForPool()
    {
        Args = default!;
        Sender = null;
        Callback = null;
    }

    /// <inheritdoc/>
    public void Dispose() => s_pool.Return(this);
}
