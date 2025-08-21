// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Security.Abstractions;
using Nalix.Common.Security.Enums;
using Nalix.Common.Security.Types;
using Nalix.Framework.Identity;
using Nalix.Network.Internal.Transport;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Connection;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed partial class Connection : IConnection
{
    #region Fields

    private readonly TransportStream _cstream;
    private readonly System.Threading.Lock _lock;
    private readonly System.Threading.CancellationTokenSource _ctokens;

    private System.Boolean _disposed;
    private System.Byte[] _encryptionKey;

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
        _ctokens = new System.Threading.CancellationTokenSource();

        _cstream = new TransportStream(socket, _ctokens);
        _cstream.Disconnected += () =>
        {
            _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        };

        _disposed = false;
        _encryptionKey = new System.Byte[0x01];
        _cstream.SetPacketCached(() => _onProcessEvent?.Invoke(this, new ConnectionEventArgs(this)));

        this.RemoteEndPoint = socket.RemoteEndPoint ?? throw new System.ArgumentNullException(nameof(socket));
        this.Id = Identifier.NewId(IdentifierType.Session);
        this.Udp = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                           .Get<UdpTransport>();
        this.Udp.Initialize(this);
        this.Tcp = new TcpTransport(this);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(Connection)}] Connection created for {this.RemoteEndPoint}");
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc />
    public IIdentifier Id { get; }

    /// <inheritdoc/>
    public IConnection.ITcp Tcp { get; }

    /// <inheritdoc/>
    public IConnection.IUdp Udp { get; }

    /// <inheritdoc />
    public System.Net.EndPoint RemoteEndPoint { get; }

    /// <inheritdoc />
    public System.Int64 UpTime => this._cstream.UpTime;

    /// <inheritdoc />
    public System.Int64 LastPingTime => this._cstream.LastPingTime;

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.Guest;

    /// <inheritdoc />
    public SymmetricAlgorithmType Encryption { get; set; } = SymmetricAlgorithmType.XTEA;

    /// <inheritdoc />
    public System.ReadOnlyMemory<System.Byte> IncomingPacket => this._cstream.PopIncoming();

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
    internal void InjectIncoming(System.Byte[] bytes) => _cstream.InjectIncoming(bytes);

    /// <inheritdoc />
    public void Close(System.Boolean force = false)
    {
        try
        {
            if (this._disposed)
            {
                return;
            }

            this._ctokens.Cancel();
            this._onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(Connection)}] Close error: {ex.Message}");
        }
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

            this._ctokens.Dispose();
            this._cstream.Dispose();

            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<UdpTransport>((UdpTransport)this.Udp);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(Connection)}] Dispose error: {ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern
}