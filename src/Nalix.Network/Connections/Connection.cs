// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Identity.Abstractions;
using Nalix.Common.Identity.Enums;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Common.Security.Enums;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Internal.Transport;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Connections;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed partial class Connection : IConnection
{
    #region Fields

    private readonly System.Threading.Lock _lock;
    private readonly ConnectionEventArgs _evtArgs;
    private readonly FramedSocketConnection _cstream;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private UdpTransport _udp;
    private System.Byte[] _secret;
    private System.Int64 _bytesSent;
    private System.Int32 _errorCount;
    private System.Int32 _closeSignaled;

    private volatile System.Boolean _disposed;

    private System.EventHandler<IConnectEventArgs> _onCloseEvent;
    private System.EventHandler<IConnectEventArgs> _onProcessEvent;
    private System.EventHandler<IConnectEventArgs> _onPostProcessEvent;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Connection"/> class with a socket, buffer allocator, and optional logger.
    /// </summary>
    /// <param name="socket">The socket used for the connection.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="socket"/> is null.</exception>
    public Connection(System.Net.Sockets.Socket socket)
    {
        _lock = new System.Threading.Lock();

        _secret = [];
        _disposed = false;

        _evtArgs = new ConnectionEventArgs(this);
        _cstream = new FramedSocketConnection(socket);
        _cstream.Cache.SetCallback(OnProcessEventBridge, this, _evtArgs);
        _cstream.SetCallback(OnCloseEventBridge, OnPostProcessEventBridge, this, _evtArgs);

        this.RemoteEndPoint = socket.RemoteEndPoint ?? throw new System.ArgumentNullException(nameof(socket));
        this.ID = Snowflake.NewId(SnowflakeType.Session);
        this.EndPoint = NetworkEndpoint.FromEndPoint(socket.RemoteEndPoint);

        this.TCP = new TcpTransport(this);

        s_logger.Debug($"[NW.{nameof(Connection)}] created remote={this.EndPoint} id={this.ID}");
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc />
    public ISnowflake ID { get; }

    /// <inheritdoc/>
    public IConnection.ITcp TCP { get; }

    /// <inheritdoc/>
    public IConnection.IUdp UDP => _udp;

    /// <inheritdoc />
    public INetworkEndpoint EndPoint { get; }

    /// <inheritdoc />
    public System.Int32 ErrorCount => _errorCount;

    /// <inheritdoc />
    public System.Net.EndPoint RemoteEndPoint { get; }

    /// <inheritdoc />
    public System.Int64 UpTime => this._cstream.Cache.Uptime;

    /// <inheritdoc />

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public IBufferLease IncomingPacket => _cstream.Cache.Incoming.Pop();

    /// <inheritdoc />
    public System.Int64 LastPingTime => this._cstream.Cache.LastPingTime;

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.NONE;

    /// <inheritdoc />
    public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.CHACHA20_POLY1305;

    /// <inheritdoc />
    public System.Byte[] Secret
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _secret;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _secret = value;
    }

    /// <summary>
    /// Gets the total number of bytes sent through this connection.
    /// </summary>
    public System.Int64 BytesSent
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Interlocked.Read(ref _bytesSent);
    }

    #endregion Properties

    #region Events

    /// <inheritdoc />

    public event System.EventHandler<IConnectEventArgs> OnCloseEvent
    {
        add => this._onCloseEvent += value;
        remove => this._onCloseEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs> OnProcessEvent
    {
        add => this._onProcessEvent += value;
        remove => this._onProcessEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs> OnPostProcessEvent
    {
        add => this._onPostProcessEvent += value;
        remove => this._onPostProcessEvent -= value;
    }

    #endregion Events

    #region Methods

    /// <inheritdoc />
    public void IncrementErrorCount() => System.Threading.Interlocked.Increment(ref _errorCount);

    /// <inheritdoc />
    public IConnection.IUdp GetOrCreateUDP()
    {
        if (_udp == null)
        {
            _udp = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                           .Get<UdpTransport>();

            _udp.Initialize(this);
        }

        return _udp;
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void InjectIncoming(System.Byte[] bytes)
    {
        if (bytes.Length == 0 || this._disposed)
        {
            return;
        }

        _cstream.Cache.LastPingTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;
        _cstream.Cache.PushIncoming(BufferLease.CopyFrom(bytes));

#if DEBUG
        s_logger.Debug($"[NW.{nameof(FramedSocketConnection)}:{InjectIncoming}] inject-bytes len={bytes.Length}");
#endif
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Close(System.Boolean force = false)
    {
        if (this._disposed)
        {
            return;
        }

        this.OnCloseEventBridge(this, new ConnectionEventArgs(this));

#if DEBUG
        s_logger.Debug($"[NW.{nameof(Connection)}:{Close}] close request id={this.ID} remote={this.EndPoint}");
#endif
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Disconnect([System.Diagnostics.CodeAnalysis.AllowNull] System.String reason = null) => this.Close(force: true);

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        lock (this._lock)
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
        }

        try
        {
            this.Disconnect();

            this._cstream.Dispose();

            if (_udp != null)
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<UdpTransport>(_udp);
            }
        }
        catch (System.Exception ex)
        {
            s_logger.Error($"[NW.{nameof(Connection)}:{Dispose}] dispose-error msg={ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern

    #region Event Bridges

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void OnCloseEventBridge(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs e)
    {
        if (System.Threading.Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        AsyncCallback.Invoke(_onCloseEvent, e.Connection, e);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void OnProcessEventBridge(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        AsyncCallback.Invoke(self._onProcessEvent, self, e);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void OnPostProcessEventBridge(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        AsyncCallback.Invoke(self._onPostProcessEvent, self, e);
    }

    #endregion Event Bridges
}
