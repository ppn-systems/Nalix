using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Security.Cryptography.Enums;
using Nalix.Common.Security.Identity;
using Nalix.Common.Security.Types;
using Nalix.Framework.Identity;
using Nalix.Network.Connection.Internal;
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
    private readonly System.Net.Sockets.Socket _socket;
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
        _socket = socket ?? throw new System.ArgumentNullException(nameof(socket));


        _cstream = new TransportStream(socket)
        {
            Disconnected = () =>
            {
                _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
            }
        };

        _disposed = false;
        _encryptionKey = new System.Byte[32];
        _cstream.SetPacketCached(() => _onProcessEvent?.Invoke(this, new ConnectionEventArgs(this)));

        this.RemoteEndPoint = socket.RemoteEndPoint ?? throw new System.ArgumentNullException(nameof(socket));
        this.Id = Identifier.NewId(IdentifierType.Session);
        this.Udp = ObjectPoolManager.Instance.Get<UdpTransport>();
        this.Udp.Initialize(this);
        this.Tcp = new TcpTransport(this);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug("[{0}] Connection created for {1}", nameof(Connection), this._socket.RemoteEndPoint?.ToString());
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Close(System.Boolean force = false)
    {
        try
        {
            if (!force && this._socket.Connected &&
               (!this._socket.Poll(1000, System.Net.Sockets.SelectMode.SelectRead) ||
                 this._socket.Available > 0))
            {
                return;
            }

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
                                    .Error("[{0}] Close error: {1}", nameof(Connection), ex.Message);
        }
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Disconnect(System.String? reason = null) => this.Close(force: true);

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

            ObjectPoolManager.Instance.Return<UdpTransport>((UdpTransport)this.Udp);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("[{0}] Dispose error: {1}", nameof(Connection), ex.Message);
        }
        finally
        {
            this._ctokens.Dispose();
            this._cstream.Dispose();
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern
}