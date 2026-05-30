// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Transport;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Hosting")]

#pragma warning disable IDE0060 // Parameters required by IConnection interface
#pragma warning disable CA1822 // Interface members cannot be static
#pragma warning disable CA2213 // _udpTransport is returned to pool, not disposed directly

namespace Nalix.Network.Connections;

/// <summary>
/// A minimal <see cref="IConnection"/> for UDP passthrough mode.
/// Carries no TCP transport and no event pipeline — exists only so that
/// <see cref="IProtocol.ProcessMessage"/> receives a valid connection with
/// a UDP transport for sending replies.
/// </summary>
/// <remarks>
/// <para>
/// Not registered in <see cref="IConnectionHub"/> or TimingWheel.
/// Keyed by remote UDP endpoint inside <c>UdpPassthroughListener</c>.
/// </para>
/// <para>
/// <b>Safety note:</b> UDP is connectionless. Protocols using this mode must
/// implement their own session/auth layer (e.g., RakNet for Minecraft Bedrock).
/// </para>
/// </remarks>
public sealed class PassthroughConnection : IConnection
{
    #region Constants

    /// <summary>
    /// Maximum number of packets allowed to be pending concurrently per connection.
    /// Prevents a single endpoint from monopolizing ThreadPool threads.
    /// </summary>
    internal const int MaxPendingPackets = 64;

    #endregion Constants

    #region Fields

    private SocketUdpTransport? _udpTransport;
    private IObjectMap<string, object>? _attributes;
    private int _pendingPackets;
    private int _isDisposed;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of <see cref="PassthroughConnection"/>.
    /// </summary>
    /// <param name="remoteEndPoint">The remote UDP endpoint.</param>
    public PassthroughConnection(EndPoint remoteEndPoint)
    {
        this.ID = Snowflake.NewId(SnowflakeType.Session);
        this.NetworkEndpoint = SocketEndpoint.FromEndPoint(remoteEndPoint as IPEndPoint);
        this.IsIdleTimeoutEnabled = false;
    }

    #endregion Constructor

    #region UDP Transport

    /// <summary>
    /// Creates and attaches a <see cref="SocketUdpTransport"/> using the shared
    /// listener socket. No-op if a transport is already attached or connection is disposed.
    /// </summary>
    /// <param name="listenerSocket">The shared UDP listener socket.</param>
    /// <param name="remoteEndPoint">The remote endpoint to send replies to.</param>
    public void BindUdp(Socket listenerSocket, IPEndPoint remoteEndPoint)
    {
        ArgumentNullException.ThrowIfNull(listenerSocket);
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        if (_udpTransport is not null || this.IsDisposed)
        {
            return;
        }

        SocketUdpTransport transport = InstanceManager.Instance
            .GetOrCreateInstance<ObjectPoolManager>()
            .Get<SocketUdpTransport>();

        transport.SetSocket(listenerSocket);

        IPEndPoint ep = remoteEndPoint;
        transport.Initialize(ref ep);

        _udpTransport = transport;
    }

    #endregion UDP Transport

    #region Pending Packet Throttle

    /// <summary>
    /// Attempts to reserve a pending-packet slot for this connection.
    /// Returns <c>false</c> if the connection is throttled.
    /// </summary>
    internal bool TryAcquirePendingPacket()
    {
        while (true)
        {
            int current = Volatile.Read(ref _pendingPackets);
            if (current >= MaxPendingPackets)
            {
                return false;
            }
            if (Interlocked.CompareExchange(ref _pendingPackets, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Releases a previously acquired pending-packet slot.
    /// </summary>
    internal void ReleasePendingPacket() => _ = Interlocked.Decrement(ref _pendingPackets);

    /// <summary>
    /// Gets the number of packets currently pending processing for this connection.
    /// </summary>
    internal int PendingPackets => Volatile.Read(ref _pendingPackets);

    #endregion Pending Packet Throttle

    #region IConnection Properties

    /// <inheritdoc />
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <inheritdoc />
    public bool IsUdpCreated => _udpTransport is not null;

    /// <inheritdoc />
    public bool IsIdleTimeoutEnabled { get; set; }

    /// <inheritdoc />
    public ISnowflake ID { get; }

    /// <inheritdoc />
    public long UpTime => 0;

    /// <inheritdoc />
    public long LastPingTime => 0;

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint { get; }

    /// <inheritdoc />
    public IObjectMap<string, object> Attributes => _attributes ??= ObjectMap<string, object>.Rent();

    /// <inheritdoc />
    public ConcurrentDictionary<ushort, object> RateLimitCache { get; } = new();

    /// <inheritdoc />
    public int ErrorCount { get; private set; }

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.NONE;

    /// <inheritdoc />
    public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;

    /// <inheritdoc />
    public Bytes32 Secret { get; set; }

    /// <inheritdoc />
    public long BytesSent => _udpTransport?.BytesSent ?? 0;

    /// <inheritdoc />
    public long BytesReceived => _udpTransport?.BytesReceived ?? 0;

    #endregion IConnection Properties

    #region TCP — Not Supported

    /// <inheritdoc />
    public IConnection.ITransport TCP => throw new NotSupportedException("Passthrough connections have no TCP transport.");

    #endregion TCP

    #region UDP

    /// <inheritdoc />
    public IConnection.ITransport UDP
    {
        get
        {
            if (_udpTransport is null)
            {
                Internal.Throw.UdpTransportNotCreated();
            }
            return _udpTransport;
        }
    }

    #endregion UDP

    #region IConnection Methods

    /// <inheritdoc />
    public void IncrementErrorCount() => this.ErrorCount++;

    /// <inheritdoc />
    public void IncrementBytesSent(int bytes) { }

    /// <inheritdoc />
    public void IncrementBytesReceived(int bytes) { }

    /// <inheritdoc />
    public void Disconnect(string? reason = null) => this.Dispose();

    #endregion IConnection Methods

    #region Events — No-op

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnCloseEvent { add { } remove { } }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnProcessEvent { add { } remove { } }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnPostProcessEvent { add { } remove { } }

    #endregion Events

    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (_udpTransport is not null)
        {
            try
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Return(_udpTransport);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            _udpTransport = null;
        }

        try { _attributes?.Return(); }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
        _attributes = null;

        GC.SuppressFinalize(this);
    }

    #endregion Dispose
}
