// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Enums;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Internal.Transport;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Connection;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed partial class Connection : IConnection
{
    #region Fields

    private readonly FramedSocketChannel _cstream;
    private readonly System.Threading.Lock _lock;

    private System.Boolean _disposed;
    private System.Byte[] _encryptionKey;
    private System.Int32 _closeSignaled;

    private System.EventHandler<IConnectEventArgs>? _onCloseEvent;
    private System.EventHandler<IConnectEventArgs>? _onProcessEvent;
    private System.EventHandler<IConnectEventArgs>? _onPostProcessEvent;

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

        _disposed = false;
        _encryptionKey = [];

        _cstream = new FramedSocketChannel(socket);
        _cstream.Cache.SetCallback(OnProcessEventBridge, this, new ConnectionEventArgs(this));
        _cstream.SetCallback(OnCloseEventBridge, OnPostProcessEventBridge, this, new ConnectionEventArgs(this));

        this.RemoteEndPoint = socket.RemoteEndPoint ?? throw new System.ArgumentNullException(nameof(socket));
        this.ID = Identifier.NewId(IdentifierType.Session);
        this.UDP = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                           .Get<UdpTransport>();
        this.UDP.Initialize(this);
        this.TCP = new TcpTransport(this);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(Connection)}] created remote={this.RemoteEndPoint} id={this.ID}");
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc />
    public IIdentifier ID { get; }

    /// <inheritdoc/>
    public IConnection.ITcp TCP { get; }

    /// <inheritdoc/>
    public IConnection.IUdp UDP { get; }

    /// <inheritdoc />
    public System.Net.EndPoint RemoteEndPoint { get; }

    /// <inheritdoc />
    public System.Int64 UpTime => this._cstream.Cache.Uptime;

    /// <inheritdoc />
    public IBufferLease? IncomingPacket => _cstream.Cache.Incoming.Pop();

    /// <inheritdoc />
    public System.Int64 LastPingTime => this._cstream.Cache.LastPingTime;

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.Guest;

    /// <inheritdoc />
    public SymmetricAlgorithmType Encryption { get; set; } = SymmetricAlgorithmType.XTEA;

    /// <inheritdoc />
    public System.Byte[] EncryptionKey
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => this._encryptionKey;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value is null || value.Length != 32)
            {
                throw new System.ArgumentException("EncryptionKey must be exactly 32 bytes.", nameof(value));
            }

            lock (this._lock)
            {
                this._encryptionKey = value;
            }
        }
    }

    #endregion Properties

    #region Events

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs>? OnCloseEvent
    {
        add => this._onCloseEvent += value;
        remove => this._onCloseEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs>? OnProcessEvent
    {
        add => this._onProcessEvent += value;
        remove => this._onProcessEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs>? OnPostProcessEvent
    {
        add => this._onPostProcessEvent += value;
        remove => this._onPostProcessEvent -= value;
    }

    #endregion Events

    #region Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
                                .Debug($"[{nameof(FramedSocketChannel)}] inject-bytes len={bytes.Length}");
#endif
    }

    /// <inheritdoc />
    public void Close(System.Boolean force = false)
    {
        if (this._disposed)
        {
            return;
        }

        this.OnCloseEventBridge(this, new ConnectionEventArgs(this));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(Connection)}] close request id={this.ID} remote={this.RemoteEndPoint}");
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Disconnect(System.String? reason = null) => this.Close(force: true);

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
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
                                    .Error($"[{nameof(Connection)}] dispose-error msg={ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern

    #region Event Bridges

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnCloseEventBridge(System.Object? sender, IConnectEventArgs e)
    {
        if (System.Threading.Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        AsyncCallback.Invoke(_onCloseEvent, e.Connection, e);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void OnProcessEventBridge(System.Object? sender, IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        AsyncCallback.Invoke(self._onProcessEvent, self, e);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void OnPostProcessEventBridge(System.Object? sender, IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        AsyncCallback.Invoke(self._onPostProcessEvent, self, e);
    }

    #endregion Event Bridges
}