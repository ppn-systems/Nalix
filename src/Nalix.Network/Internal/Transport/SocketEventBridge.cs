// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Environment.Memory;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Abstractions;
using Nalix.Network.Options;

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Bridges transport-level frame events from <see cref="SocketConnection"/>
/// into the connection-level event pipeline (<see cref="AsyncCallback"/> →
/// process / post-process / close event handlers).
///
/// <para>This class owns the per-connection throttle state
/// (<c>_pendingProcessCallbacks</c>) and all callback delegate references
/// that were previously held directly by <see cref="SocketConnection"/>.
/// By moving them here the transport layer no longer needs to know about
/// <see cref="ConnectionEventArgs"/>, <see cref="AsyncCallback"/>, or
/// protocol-level event semantics.</para>
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
internal sealed class SocketEventBridge : ITransportEventSink
{
    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
    private static readonly NetworkCallbackOptions s_opts = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();

    private readonly EventHandler<IConnectEventArgs>? _callbackProcess;
    private readonly EventHandler<IConnectEventArgs>? _callbackPost;
    private readonly EventHandler<IConnectEventArgs>? _callbackClose;

    /// <summary>
    /// Per-connection pending callback counter for Layer 1 throttle.
    /// Incremented when a frame is accepted, decremented when the
    /// callback completes on the ThreadPool.
    /// </summary>
    private int _pendingProcessCallbacks;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new bridge that will dispatch transport events
    /// through the specified callback delegates.
    /// </summary>
    /// <param name="callbackProcess">Process (receive) event handler.</param>
    /// <param name="callbackPost">Post-process (send-complete) event handler.</param>
    /// <param name="callbackClose">Close event handler.</param>
    internal SocketEventBridge(EventHandler<IConnectEventArgs>? callbackProcess, EventHandler<IConnectEventArgs>? callbackPost, EventHandler<IConnectEventArgs>? callbackClose)
    {
        _callbackProcess = callbackProcess;
        _callbackPost = callbackPost;
        _callbackClose = callbackClose;
    }

    #endregion Constructor

    #region Properties

    /// <summary>
    /// Returns the number of packets dispatched to <see cref="AsyncCallback"/>
    /// that have not yet been processed by the protocol handler.
    /// Used by diagnostics and the per-connection throttle check.
    /// </summary>
    public int PendingPackets => Volatile.Read(ref _pendingProcessCallbacks);

    #endregion Properties

    #region ITransportEventSink

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool OnFrameReceived(IConnection connection, BufferLease lease, bool isReliable)
    {
        /*
         * [Layer 1 Throttle Check]
         * We check the number of packets already in the pipeline for this connection.
         * If the connection is flooding, we return false so the transport drops the
         * packet immediately — before it ever reaches AsyncCallback or the ThreadPool.
         */
        int pending = Interlocked.Increment(ref _pendingProcessCallbacks);
        if (pending > s_opts.MaxPerConnectionPendingPackets)
        {
            _ = Interlocked.Decrement(ref _pendingProcessCallbacks);
            return false;
        }

        ConnectionEventArgs? args = (connection as Connection)?.AcquireEventArgs()
                                    ?? s_pool.Get<ConnectionEventArgs>();

        args.Initialize(lease, connection);

        if (!AsyncCallback.Invoke(_callbackProcess, connection, args, releasePendingPacketOnCompletion: true))
        {
            _ = Interlocked.Decrement(ref _pendingProcessCallbacks);
            _ = args.ExchangeLease(null);
            args.Dispose();
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnFrameSent(IConnection connection)
    {
        ConnectionEventArgs? args = (connection as Connection)?.AcquireEventArgs()
                                    ?? s_pool.Get<ConnectionEventArgs>();
        args.Initialize(connection);

        if (!AsyncCallback.Invoke(_callbackPost, connection, args))
        {
            args.Dispose();
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTransportClosed(IConnection connection)
    {
        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(connection);

        if (!AsyncCallback.InvokeHighPriority(_callbackClose, connection, args))
        {
            args.Dispose();
        }
    }

    #endregion ITransportEventSink

    #region Throttle Helpers

    /// <summary>
    /// Called by the protocol handler (via <see cref="Pooling.IPooledConnectContextPool"/>)
    /// after each packet has been fully processed. Decrements the per-connection
    /// pending counter so the receive loop can accept the next packet from this
    /// connection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleasePendingPacket() => Interlocked.Decrement(ref _pendingProcessCallbacks);

#if DEBUG
    /// <summary>
    /// Manually increments the pending callback counter.
    /// Used by test injection paths to respect the connection throttle.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void IncrementPendingCallbacks() => Interlocked.Increment(ref _pendingProcessCallbacks);
#endif

    #endregion Throttle Helpers
}
