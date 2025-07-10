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

    private readonly IIdentifier _id;
    private readonly ILogger? _logger;
    private readonly IConnection.ITcp _tcp;
    private readonly IConnection.IUdp _udp;
    private readonly System.Threading.Lock _lock;
    private readonly System.Net.EndPoint _endPoint;
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
        _lock = new System.Threading.Lock();
        _id = Token.NewId(TokenType.Session);
        _ctokens = new System.Threading.CancellationTokenSource();

        _logger = logger;
        _socket = socket ?? throw new System.ArgumentNullException(nameof(socket));
        _endPoint = socket.RemoteEndPoint ?? throw new System.ArgumentNullException(nameof(socket));

        _cstream = new Transport.TransportStream(socket, bufferAllocator, _logger)
        {
            Disconnected = () =>
            {
                _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
            }
        };

        _cstream.SetPacketCached(() => _onProcessEvent?.Invoke(this, new ConnectionEventArgs(this)));

        _disposed = false;
        _encryptionKey = new System.Byte[32];

        _tcp = new TcpTransport(this);
        _udp = new UdpTransport(this);

        _logger?.Debug("[{0}] Connection created for {1}",
            nameof(Connection), _socket.RemoteEndPoint?.ToString());
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc />
    public IIdentifier Id => _id;

    /// <inheritdoc/>
    public IConnection.ITcp Tcp => _tcp;

    /// <inheritdoc/>
    public IConnection.IUdp Udp => _udp;

    /// <inheritdoc />
    public System.Net.EndPoint RemoteEndPoint => _endPoint;

    /// <inheritdoc />
    public System.Int64 UpTime => _cstream.UpTime;

    /// <inheritdoc />
    public System.Int64 LastPingTime => _cstream.LastPingTime;

    /// <inheritdoc />
    public System.Byte[] EncryptionKey
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _encryptionKey;
        set
        {
            if (value is null || value.Length != 32)
                throw new System.ArgumentException("EncryptionKey must be exactly 16 bytes.", nameof(value));

            lock (_lock)
            {
                _encryptionKey = value;
            }
        }
    }

    /// <inheritdoc />
    public System.ReadOnlyMemory<System.Byte> IncomingPacket => _cstream.GetIncomingPackets();

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.Guest;

    /// <inheritdoc />
    public SymmetricAlgorithmType Encryption { get; set; } = SymmetricAlgorithmType.XTEA;

    #endregion Properties

    #region Events

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs>? OnCloseEvent
    {
        add => _onCloseEvent += value;
        remove => _onCloseEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs>? OnProcessEvent
    {
        add => _onProcessEvent += value;
        remove => _onProcessEvent -= value;
    }

    /// <inheritdoc />
    public event System.EventHandler<IConnectEventArgs>? OnPostProcessEvent
    {
        add => _onPostProcessEvent += value;
        remove => _onPostProcessEvent -= value;
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
            if (!force && _socket.Connected &&
               (!_socket.Poll(1000, System.Net.Sockets.SelectMode.SelectRead) || _socket.Available > 0))
                return;

            if (_disposed) return;

            _ctokens.Cancel();
            _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (System.Exception ex)
        {
            _logger?.Error("[{0}] Close error: {1}", nameof(Connection), ex.Message);
        }
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Disconnect(System.String? reason = null) => Close(force: true);

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        try
        {
            this.Disconnect();
        }
        catch (System.Exception ex)
        {
            _logger?.Error("[{0}] Dispose error: {1}", nameof(Connection), ex.Message);
        }
        finally
        {
            _socket.Dispose();
            _ctokens.Dispose();
            _cstream.Dispose();
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern
}