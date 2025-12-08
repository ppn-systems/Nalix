// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Enums;
using Nalix.Common.Logging;
using Nalix.Framework.Identity;
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
    private readonly FramedSocketChannel _cstream;

    private System.Byte[] _secret;
    private System.Boolean _disposed;
    private System.Int32 _closeSignaled;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private System.EventHandler<IConnectEventArgs> _onCloseEvent;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private System.EventHandler<IConnectEventArgs> _onProcessEvent;

    [System.Diagnostics.CodeAnalysis.AllowNull]
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
        _cstream = new FramedSocketChannel(socket);
        _cstream.Cache.SetCallback(OnProcessEventBridge, this, _evtArgs);
        _cstream.SetCallback(OnCloseEventBridge, OnPostProcessEventBridge, this, _evtArgs);

        this.ID = Snowflake.NewId(SnowflakeType.Session);
        this.EndPoint = EndpointToken.FromEndPoint(socket.RemoteEndPoint);
        this.UDP = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                           .Get<UdpTransport>();
        this.UDP.Initialize(this);
        this.TCP = new TcpTransport(this);
        this.RemoteEndPoint = socket.RemoteEndPoint ?? throw new System.ArgumentNullException(nameof(socket));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(Connection)}] created remote={this.EndPoint} id={this.ID}");
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc />
    public ISnowflake ID { get; }

    /// <inheritdoc/>
    public IConnection.ITcp TCP { get; }

    /// <inheritdoc/>
    public IConnection.IUdp UDP { get; }

    /// <inheritdoc />
    public System.Net.EndPoint RemoteEndPoint { get; }

    /// <inheritdoc />
    public INetworkEndpoint EndPoint { get; }

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
        set
        {
            if (value is null || value.Length != 32)
            {
                throw new System.ArgumentException("Secret must be exactly 32 bytes.", nameof(value));
            }

            // Copy to protect internal secret from external mutation.
            System.Byte[] copy = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(32);
            try
            {
                System.Buffer.BlockCopy(value, 0, copy, 0, 32);
                lock (_lock)
                {
                    // return previous if it was pooled? we use Array.Empty or rented - keep simple:
                    _secret = copy.Length == 32 ? copy : [.. copy[..32]];
                }
            }
            finally
            {
                // If copy isn't used as internal storage, return it. But we used it as internal storage so do not return.
            }
        }
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
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(FramedSocketChannel)}:{InjectIncoming}] inject-bytes len={bytes.Length}");
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
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(Connection)}:{Close}] close request id={this.ID} remote={this.EndPoint}");
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

            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<UdpTransport>((UdpTransport)this.UDP);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(Connection)}:{Dispose}] dispose-error msg={ex.Message}");
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