// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// Provides a per-connection protocol pipeline that routes inbound frames through a fixed set of stages:
/// Handshake (X25519) -> Decrypt -> Decompress -> Application.
/// </summary>
/// <remarks>
/// <para>
/// This type is designed to be allocated per connection. However, it is also safe to reuse via pooling
/// as long as callers follow the lifecycle contract:
/// </para>
/// <list type="number">
/// <item><description>Create or rent an instance from a pool.</description></item>
/// <item><description>Call <see cref="Initialize(IConnection, IProtocol)"/>.</description></item>
/// <item><description>Call <see cref="Bind"/> to attach to <see cref="IConnection.OnProcessEvent"/>.</description></item>
/// <item><description>On connection close: call <see cref="ResetForPool"/> (or at least <see cref="Unbind"/>).</description></item>
/// <item><description>Before returning to pool: call <see cref="ResetForPool"/>.</description></item>
/// </list>
/// <para>
/// Ownership rule: the pipeline owns disposal of <see cref="IConnectEventArgs"/> and its
/// <see cref="IConnectEventArgs.Lease"/>. Individual stages must not dispose these objects.
/// </para>
/// </remarks>
[DebuggerDisplay("Disposed={_disposed != 0}, Bound={_bound != 0}")]
internal sealed class ProtocolPipeline : IPoolable
{
    #region Static

    private static readonly ProtocolX25519 s_handshake = InstanceManager.Instance.GetOrCreateInstance<ProtocolX25519>();
    private static readonly ProtocolDecrypt s_decrypt = InstanceManager.Instance.GetOrCreateInstance<ProtocolDecrypt>();
    private static readonly ProtocolDecompress s_decompress = InstanceManager.Instance.GetOrCreateInstance<ProtocolDecompress>();

    #endregion Static

    #region Fields

    private IProtocol? _application;
    private IConnection? _connection;

    private int _bound;
    private int _disposed;

    #endregion Fields

    /// <summary>
    /// Initializes this instance for a specific connection and protocol stage set.
    /// </summary>
    /// <param name="connection">
    /// The connection that owns this pipeline. This instance will subscribe to <see cref="IConnection.OnProcessEvent"/>
    /// when <see cref="Bind"/> is called.
    /// </param>
    /// <param name="application">
    /// The final application protocol stage. This is where your main protocol routes and dispatches messages.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the pipeline has been disposed and not reset for pooling.
    /// </exception>
    /// <remarks>
    /// This method must be called before <see cref="Bind"/>. It is intentionally separate from construction
    /// to allow pooling/reuse.
    /// </remarks>
    public void Initialize(IConnection connection, IProtocol application)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(application);

        // A pipeline instance must not be re-initialized while still bound.
        if (Volatile.Read(ref _bound) != 0)
        {
            throw new InvalidOperationException("ProtocolPipeline is already bound; call Unbind() before reinitializing.");
        }

        _connection = connection;
        _application = application;
    }

    /// <summary>
    /// Subscribes this pipeline to the connection's <see cref="IConnection.OnProcessEvent"/> event.
    /// After binding, all inbound frames will be routed through <see cref="ProcessMessage"/>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the pipeline has not been initialized, or if it is already bound.
    /// </exception>
    /// <remarks>
    /// This method is idempotent for callers that respect the contract:
    /// binding twice is treated as a programming error because it would double-subscribe.
    /// </remarks>
    [DebuggerStepThrough]
    public void Bind()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (_connection is null || _application is null)
        {
            throw new InvalidOperationException("ProtocolPipeline must be initialized before binding.");
        }

        if (Interlocked.CompareExchange(ref _bound, 1, 0) != 0)
        {
            throw new InvalidOperationException("ProtocolPipeline is already bound.");
        }

        _connection.OnProcessEvent += this.ProcessMessage;
    }

    /// <summary>
    /// Unsubscribes this pipeline from the connection's <see cref="IConnection.OnProcessEvent"/> event.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times. It is typically invoked during connection teardown
    /// (e.g., close/dispose path) to prevent memory leaks and duplicate callbacks.
    /// </remarks>
    [DebuggerStepThrough]
    public void Unbind()
    {
        IConnection? connection = _connection;
        if (connection is null)
        {
            _ = Interlocked.Exchange(ref _bound, 0);
            return;
        }

        // Ensure we only unbind once.
        if (Interlocked.Exchange(ref _bound, 0) == 0)
        {
            return;
        }

        connection.OnProcessEvent -= this.ProcessMessage;
    }

    /// <summary>
    /// Routes inbound frames through the pipeline:
    /// Handshake gate -> Decrypt -> Decompress -> Application.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments containing the connection and payload buffer lease.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this pipeline instance has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method owns disposal of <paramref name="args"/> and <paramref name="args"/>.<see cref="IConnectEventArgs.Lease"/>.
    /// </para>
    /// <para>
    /// Individual protocol stages must not dispose the args or lease.
    /// </para>
    /// </remarks>
    public void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        // Local copies reduce race risk if ResetForPool is called incorrectly.
        IProtocol? application = _application;

        if (application is null)
        {
            // Not initialized or already reset; drop safely.
            args.Lease?.Dispose();
            args.Dispose();
            return;
        }

        try
        {
            // Stage 1: Handshake gate. Only handshake frames are processed until established.
            if (!ProtocolX25519.IsEstablished(args.Connection))
            {
                s_handshake.ProcessMessage(sender, args);
                return;
            }

            // Stage 2: Decrypt stage.
            s_decrypt.ProcessMessage(sender, args);

            // Stage 3: Decompress stage.
            s_decompress.ProcessMessage(sender, args);

            // Stage 4: Application stage.
            application.ProcessMessage(sender, args);
        }
        finally
        {
            // Ownership rule: pipeline disposes args/lease exactly once.
            args.Lease?.Dispose();
            args.Dispose();
        }
    }

    /// <summary>
    /// Clears all references and resets state so that this instance can be safely returned to an object pool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers must ensure the pipeline has been unbound before calling this method. The recommended sequence is:
    /// </para>
    /// <code>
    /// pipeline.Dispose();
    /// pipeline.ResetForPool();
    /// pool.Return(pipeline);
    /// </code>
    /// <para>
    /// Failing to reset references may keep the connection (and related object graph) alive longer than intended.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    public void ResetForPool()
    {
        // If still bound, unbind defensively.
        this.Unbind();

        _connection = null;
        _application = null;

        // Reset flags so the pooled instance can be re-used.
        _ = Interlocked.Exchange(ref _disposed, 0);
        _ = Interlocked.Exchange(ref _bound, 0);
    }
}
