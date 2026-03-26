// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Security;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Connections;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed partial class Connection : IConnection
{
    #region Fields

    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>()!;
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private readonly Lock _lock;
    private readonly ConnectionEventArgs _evtArgs;
    private readonly FramedSocketConnection _cstream;

    private UdpTransport? _udp;
    private int _errorCount;
    private int _closeSignaled;
    private long _bytesSent;

    private volatile bool _disposed;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Connection"/> class with a socket, buffer allocator, and optional logger.
    /// </summary>
    /// <param name="socket">The socket used for the connection.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="socket"/> is null.</exception>
    public Connection(Socket socket)
    {
        _lock = new Lock();
        Secret = [];
        _disposed = false;

        ID = Snowflake.NewId(SnowflakeType.Session);
        NetworkEndpoint = Endpoint.FromEndPoint(socket.RemoteEndPoint ?? throw new InvalidOperationException("Socket does not expose a remote endpoint."));

        _evtArgs = new ConnectionEventArgs(this);
        _cstream = new FramedSocketConnection(socket);

        _cstream.SetCallback(this, _evtArgs, OnCloseEventBridge, OnPostProcessEventBridge, OnProcessEventBridge);

        TCP = new TcpTransport(this);
        Attributes = ObjectMap<string, object>.Rent();

        s_logger.Debug($"[NW.{nameof(Connection)}] created remote={NetworkEndpoint} id={ID}");
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc />
    public ISnowflake ID { get; }

    /// <inheritdoc/>
    public IConnection.ITcp TCP { get; }

    /// <inheritdoc/>
    public IConnection.IUdp UDP => _udp ?? throw new InvalidOperationException("UDP transport has not been created yet.");

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint { get; }

    /// <inheritdoc />
    public IObjectMap<string, object> Attributes { get; }

    /// <inheritdoc />
    public int ErrorCount => _errorCount;

    /// <inheritdoc />
    public long UpTime => _cstream.Cache.Uptime;

    /// <inheritdoc />
    public long LastPingTime => _cstream.Cache.LastPingTime;

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.NONE;

    /// <inheritdoc />
    public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;

    /// <inheritdoc />
    public byte[] Secret
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set;
    }

    /// <summary>
    /// Gets the total number of bytes sent through this connection.
    /// </summary>
    public long BytesSent
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _bytesSent);
    }

    #endregion Properties

    #region Events

    /// <inheritdoc />

    public event EventHandler<IConnectEventArgs> OnCloseEvent
    {
        add => _onCloseEvent += value;
        remove => _onCloseEvent -= value;
    }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs> OnProcessEvent
    {
        add => _onProcessEvent += value;
        remove => _onProcessEvent -= value;
    }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs> OnPostProcessEvent
    {
        add => _onPostProcessEvent += value;
        remove => _onPostProcessEvent -= value;
    }

    #endregion Events

    #region Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Close(bool force = false)
    {
        if (_disposed)
        {
            return;
        }

        OnCloseEventBridge(this, new ConnectionEventArgs(this));

#if DEBUG
        s_logger.Debug($"[NW.{nameof(Connection)}:{Close}] close request id={ID} remote={NetworkEndpoint}");
#endif
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public void Disconnect(string? reason = null) => Close(force: true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddBytesSent(int count) => _ = Interlocked.Add(ref _bytesSent, count);

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        try
        {
            Attributes.Return();

            Disconnect();

            _cstream.Dispose();

            if (_udp != null)
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return(_udp);
            }
        }
        catch (Exception ex)
        {
            s_logger.Error($"[NW.{nameof(Connection)}:{Dispose}] dispose-error msg={ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern

    #region Event Bridges

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private void OnCloseEventBridge(
        object? sender,
        IConnectEventArgs e)
    {
        if (Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        // Close events bypas backpressure — cleanup must never be delayed
        _ = Internal.Transport.AsyncCallback.InvokeHighPriority(_onCloseEvent, e.Connection, e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static void OnProcessEventBridge(
        object? sender,
        IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        _ = Internal.Transport.AsyncCallback.Invoke(self._onProcessEvent, self, e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static void OnPostProcessEventBridge(
        object? sender,
        IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        _ = Internal.Transport.AsyncCallback.Invoke(self._onPostProcessEvent, self, e);
    }

    #endregion Event Bridges
}
