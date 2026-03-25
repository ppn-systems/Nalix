// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Security;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Network.Internal.Transport;
using Nalix.Shared.Memory.Objects;

namespace Nalix.Network.Connections;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed partial class Connection : IConnection
{
    #region Fields

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private readonly System.Threading.Lock _lock;
    private readonly ConnectionEventArgs _evtArgs;
    private readonly FramedSocketConnection _cstream;

    private UdpTransport _udp;
    private int _errorCount;
    private int _closeSignaled;
    private long _bytesSent;

    private volatile bool _disposed;

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
        Secret = [];
        _disposed = false;

        ID = Snowflake.NewId(SnowflakeType.Session);
        NetworkEndpoint = Endpoint.FromEndPoint(socket.RemoteEndPoint);

        _evtArgs = new ConnectionEventArgs(this);
        _cstream = new FramedSocketConnection(socket);

        _cstream.SetCallback(this, _evtArgs, OnCloseEventBridge, OnPostProcessEventBridge, OnProcessEventBridge);

        TCP = new TcpTransport(this);

        s_logger.Debug($"[NW.{nameof(Connection)}] created remote={NetworkEndpoint} id={ID}");
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
    public INetworkEndpoint NetworkEndpoint { get; }

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
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set;
    }

    /// <summary>
    /// Gets the total number of bytes sent through this connection.
    /// </summary>
    public long BytesSent
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
        add => _onCloseEvent += value;
        remove => _onCloseEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs> OnProcessEvent
    {
        add => _onProcessEvent += value;
        remove => _onProcessEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs> OnPostProcessEvent
    {
        add => _onPostProcessEvent += value;
        remove => _onPostProcessEvent -= value;
    }

    #endregion Events

    #region Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Disconnect([System.Diagnostics.CodeAnalysis.AllowNull] string reason = null) => Close(force: true);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void AddBytesSent(int count) => _ = System.Threading.Interlocked.Add(ref _bytesSent, count);

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
            Disconnect();

            _cstream.Dispose();

            if (_udp != null)
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(_udp);
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
        [System.Diagnostics.CodeAnalysis.AllowNull] object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs e)
    {
        if (System.Threading.Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        // Close events bypas backpressure — cleanup must never be delayed
        _ = AsyncCallback.InvokeHighPriority(_onCloseEvent, e.Connection, e);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void OnProcessEventBridge(
        [System.Diagnostics.CodeAnalysis.AllowNull] object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        _ = AsyncCallback.Invoke(self._onProcessEvent, self, e);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void OnPostProcessEventBridge(
        [System.Diagnostics.CodeAnalysis.AllowNull] object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        _ = AsyncCallback.Invoke(self._onPostProcessEvent, self, e);
    }

    #endregion Event Bridges
}
