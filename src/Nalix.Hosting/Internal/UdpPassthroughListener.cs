// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Memory;
using Nalix.Network.Connections;
using Nalix.Network.Listeners.Udp;

namespace Nalix.Hosting.Internal;

/// <summary>
/// Provides a UDP listener that bypasses Nalix frame decoding, session-token
/// resolution, and security-layer processing. Received datagrams are forwarded
/// directly to the configured protocol as raw bytes.
/// </summary>
/// <remarks>
/// <para>
/// This listener is intended for protocols that implement their own framing and
/// session management over UDP, such as Minecraft Bedrock (RakNet).
/// </para>
/// <para>
/// Each unique remote UDP endpoint is tracked as a lightweight
/// <see cref="PassthroughConnection"/>. These connections are not registered
/// in the <see cref="IConnectionHub"/>.
/// </para>
/// <para>
/// Idle connections are automatically evicted after a configurable timeout.
/// Per-connection backpressure prevents a single endpoint from monopolizing
/// the ThreadPool.
/// </para>
/// <para>
/// <b>Safety note:</b> Because Nalix session tokens, replay protection, and
/// authentication hooks are all bypassed, the protocol layer is fully responsible
/// for its own security model.
/// </para>
/// </remarks>
internal sealed class UdpPassthroughListener : UdpListenerBase
{
    #region Constants

    /// <summary>Default idle timeout before a virtual connection is evicted.</summary>
    private static readonly TimeSpan s_idleTimeout = TimeSpan.FromMinutes(2);

    /// <summary>How often the idle-eviction sweep runs.</summary>
    private static readonly TimeSpan s_sweepInterval = TimeSpan.FromSeconds(30);

    #endregion Constants

    #region Fields

    private readonly ConcurrentDictionary<EndPoint, ConnectionEntry> _connections = new();
    private Timer? _sweepTimer;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpPassthroughListener"/> class.
    /// </summary>
    public UdpPassthroughListener(IProtocol protocol, IConnectionHub hub) : base(protocol, hub) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpPassthroughListener"/> class.
    /// </summary>
    public UdpPassthroughListener(ushort port, IProtocol protocol, IConnectionHub hub) : base(port, protocol, hub) { }

    #endregion Constructors

    #region Lifecycle

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sweepTimer?.Dispose();
            _sweepTimer = null;

            foreach (ConnectionEntry entry in _connections.Values)
            {
                entry.Connection.Dispose();
            }
            _connections.Clear();
        }

        base.Dispose(disposing);
    }

    #endregion Lifecycle

    #region ProcessDatagram

    /// <summary>
    /// Bypasses all Nalix datagram processing and forwards the raw payload
    /// directly to the configured protocol.
    /// </summary>
    protected override void ProcessDatagram(BufferLease lease, EndPoint remoteEndPoint)
    {
        if (lease is null || remoteEndPoint is null)
        {
            lease?.Dispose();
            return;
        }

        if (remoteEndPoint is not IPEndPoint ipEndPoint)
        {
            lease.Dispose();
            return;
        }

        // Normalize IPv4-mapped IPv6 to plain IPv4 for consistent keying.
        if (ipEndPoint.Address.IsIPv4MappedToIPv6)
        {
            ipEndPoint = new IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
        }

        // Lazily start the idle-eviction timer on first datagram.
        if (_sweepTimer is null)
        {
            _ = Interlocked.CompareExchange(ref this._sweepTimer,
                new Timer(this.SweepIdleConnections, null, s_sweepInterval, s_sweepInterval),
                null);
        }

        ConnectionEntry entry = _connections.GetOrAdd(
            ipEndPoint,
            static ep => new ConnectionEntry(ep));

        // Touch last-active timestamp for idle eviction.
        entry.Touch();

        PassthroughConnection connection = entry.Connection;

        if (connection.IsDisposed)
        {
            // Stale entry from a previous sweep race — replace it.
            if (_connections.TryUpdate(ipEndPoint, new ConnectionEntry(ipEndPoint), entry))
            {
                entry = _connections[ipEndPoint];
                connection = entry.Connection;
            }
            else
            {
                // Another thread already replaced it; re-read.
                connection = _connections[ipEndPoint].Connection;
            }
        }

        connection.BindUdp(this.ListenerSocket!, ipEndPoint);

        // Per-connection backpressure: drop if too many packets are pending.
        if (!connection.TryAcquirePendingPacket())
        {
            lease.Dispose();
            return;
        }

        lease.IsReliable = false;

        // Offload to ThreadPool so the receive loop is not blocked by protocol processing.
#pragma warning disable CA2000 // Disposal ownership transferred to ThreadPool callback
        PassthroughArgs args = new(lease, connection, this);
#pragma warning restore CA2000

        if (!ThreadPool.UnsafeQueueUserWorkItem(s_processCallback, args))
        {
            connection.ReleasePendingPacket();
            args.Dispose();
        }
    }

    #endregion ProcessDatagram

    #region Async Callback

    private static readonly WaitCallback s_processCallback = static state =>
    {
        if (state is not PassthroughArgs args)
        {
            return;
        }

        PassthroughConnection? connection = args.Connection as PassthroughConnection;

        try
        {
            if (connection is null || connection.IsDisposed)
            {
                return;
            }

            args.Listener?.ProcessFrame(args.Listener, args);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            connection?.IncrementErrorCount();
        }
        finally
        {
            connection?.ReleasePendingPacket();
            args.Dispose();
        }
    };

    #endregion Async Callback

    #region ProcessFrame

    /// <inheritdoc />
    public override void ProcessFrame(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        this.Protocol.ProcessMessage(sender, args);
    }

    /// <inheritdoc />
    public override bool IsAuthenticated(IConnection connection, EndPoint remoteEndPoint, ReadOnlySpan<byte> payload) => true;

    #endregion ProcessFrame

    #region Idle Eviction

    private void SweepIdleConnections(object? state)
    {
        long cutoffMs = global::System.Environment.TickCount64 - (long)s_idleTimeout.TotalMilliseconds;

        foreach (KeyValuePair<EndPoint, ConnectionEntry> kvp in _connections)
        {
            if (kvp.Value.LastActiveMs < cutoffMs)
            {
                if (_connections.TryRemove(kvp.Key, out ConnectionEntry? removed))
                {
                    removed?.Connection.Dispose();
                }
            }
        }
    }

    #endregion Idle Eviction

    #region ConnectionEntry

    /// <summary>
    /// Wraps a <see cref="PassthroughConnection"/> with a timestamp for idle eviction.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.CodeAnalysis", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "Connection lifetime is managed by the listener's sweep timer.")]
    private sealed class ConnectionEntry
    {
        public readonly PassthroughConnection Connection;
        private long _lastActiveMs;

        public ConnectionEntry(EndPoint remoteEndPoint)
        {
            this.Connection = new PassthroughConnection(remoteEndPoint);
            _lastActiveMs = global::System.Environment.TickCount64;
        }

        /// <summary>Updates the last-active timestamp.</summary>
        public void Touch() => Volatile.Write(ref _lastActiveMs, global::System.Environment.TickCount64);

        /// <summary>Gets the last-active timestamp in milliseconds.</summary>
        public long LastActiveMs => Volatile.Read(ref _lastActiveMs);
    }

    #endregion ConnectionEntry

    #region PassthroughArgs

    /// <summary>
    /// Minimal <see cref="IConnectEventArgs"/> for UDP passthrough mode.
    /// </summary>
    private sealed class PassthroughArgs : IConnectEventArgs
    {
        private IBufferLease? _lease;

        public PassthroughArgs(IBufferLease lease, IConnection connection, UdpPassthroughListener listener)
        {
            _lease = lease;
            this.Connection = connection;
            this.Listener = listener;
        }

        public IConnection Connection { get; }

        public IBufferLease? Lease => _lease;

        public INetworkEndpoint? NetworkEndpoint => this.Connection.NetworkEndpoint;

        /// <summary>Back-reference to the listener for async callback dispatch.</summary>
        internal UdpPassthroughListener Listener { get; }

        public void Dispose()
        {
            IBufferLease? lease = _lease;
            _lease = null;
            lease?.Dispose();
        }
    }

    #endregion PassthroughArgs
}
