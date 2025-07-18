using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Security.Cryptography;
using Nalix.Common.Security.Identity;
using Nalix.Common.Security.Types;
using Nalix.Framework.Identity;

namespace Nalix.Network.Connection;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed partial class Connection : IConnection
{
    #region Fields

    private readonly ILogger? _logger;
    private readonly System.Threading.Lock _lock;
    private readonly System.Net.Sockets.Socket _socket;
    private readonly Transport.TransportStream _cstream;
    private readonly System.Threading.CancellationTokenSource _ctokens;

    private System.EventHandler<IConnectEventArgs>? _onCloseEvent;
    private System.EventHandler<IConnectEventArgs>? _onProcessEvent;
    private System.EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    private System.Boolean _disposed;
    private System.Byte[] _encryptionKey;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Connection"/> class with a socket, buffer allocator, and optional logger.
    /// </summary>
    /// <param name="socket">The socket used for the connection.</param>
    /// <param name="bufferAllocator">The buffer pool used for data allocation.</param>
    /// <param name="logger">The logger used for logging connection events. If null, no logging will occur.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="socket"/> is null.</exception>
    public Connection(System.Net.Sockets.Socket socket, IBufferPool bufferAllocator, ILogger? logger = null)
    {
        this._lock = new System.Threading.Lock();
        this.Id = Identifier.NewId(IdentifierType.Session);
        this._ctokens = new System.Threading.CancellationTokenSource();

        this._logger = logger;
        this._socket = socket ?? throw new System.ArgumentNullException(nameof(socket));
        this.RemoteEndPoint = socket.RemoteEndPoint ?? throw new System.ArgumentNullException(nameof(socket));

        this._cstream = new Transport.TransportStream(socket, bufferAllocator, this._logger)
        {
            Disconnected = () =>
            {
                this._onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
            }
        };

        this._cstream.SetPacketCached(() => this._onProcessEvent?.Invoke(this, new ConnectionEventArgs(this)));

        this._disposed = false;

        this._encryptionKey = new System.Byte[32];

        this.Tcp = new TcpTransport(this);
        this.Udp = new UdpTransport(this);

        this._logger?.Debug("[{0}] Connection created for {1}",
            nameof(Connection), this._socket.RemoteEndPoint?.ToString());
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
    public System.Byte[] EncryptionKey
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => this._encryptionKey;
        set
        {
            if (value is null || value.Length != 32)
            {
                throw new System.ArgumentException("EncryptionKey must be exactly 16 bytes.", nameof(value));
            }

            lock (this._lock)
            {
                this._encryptionKey = value;
            }
        }
    }

    /// <inheritdoc />
    public System.ReadOnlyMemory<System.Byte> IncomingPacket => this._cstream.GetIncomingPackets();

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.Guest;

    /// <inheritdoc />
    public SymmetricAlgorithmType Encryption { get; set; } = SymmetricAlgorithmType.XTEA;

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
    public void Close(System.Boolean force = false)
    {
        try
        {
            if (!force && this._socket.Connected &&
               (!this._socket.Poll(1000, System.Net.Sockets.SelectMode.SelectRead) || this._socket.Available > 0))
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
            this._logger?.Error("[{0}] Close error: {1}", nameof(Connection), ex.Message);
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
        }
        catch (System.Exception ex)
        {
            this._logger?.Error("[{0}] Dispose error: {1}", nameof(Connection), ex.Message);
        }
        finally
        {
            this._socket.Dispose();
            this._ctokens.Dispose();
            this._cstream.Dispose();
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern
}