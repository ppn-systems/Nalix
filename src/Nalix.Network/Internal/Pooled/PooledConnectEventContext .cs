// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Objects;

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// Represents a reusable (pooled) context for invoking a connection event callback.
/// </summary>
/// <remarks>
/// This type is designed to minimize allocations by reusing instances via an object pool.
/// It temporarily stores the event sender, arguments, and callback delegate during invocation.
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class PooledConnectEventContext : IPoolable
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    /// <summary>
    /// Gets or sets the event sender.
    /// </summary>
    public System.Object Sender;

    /// <summary>
    /// Gets or sets the connection event arguments.
    /// </summary>
    public IConnectEventArgs Args;

    /// <summary>
    /// Gets or sets the callback delegate to invoke.
    /// </summary>
    public System.EventHandler<IConnectEventArgs> Callback;

    /// <summary>
    /// Initializes the context with the specified callback, sender, and arguments.
    /// </summary>
    /// <param name="callback">The event handler to invoke.</param>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The event arguments.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Initialize(System.EventHandler<IConnectEventArgs> callback, System.Object sender, IConnectEventArgs args)
    {
        Args = args;
        Sender = sender;
        Callback = callback;
    }

    /// <inheritdoc/>
    public static PooledConnectEventContext Get() => s_pool.Get<PooledConnectEventContext>();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetForPool()
    {
        Args = null;
        Sender = null;
        Callback = null;
    }

    /// <inheritdoc/>
    public void Dispose() => s_pool.Return(this);
}