using Nalix.Common.Caching;
using Nalix.Common.Compression;
using Nalix.Common.Connection;
using Nalix.Common.Cryptography;
using Nalix.Common.Identity;
using Nalix.Common.Logging;
using Nalix.Common.Security;
using Nalix.Identifiers;
using Nalix.Network.Connection.Transport;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.Connection;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed partial class Connection : IConnection
{
    #region Fields

    private readonly Lock _lock;
    private readonly Base36Id _id;
    private readonly Socket _socket;
    private readonly ILogger? _logger;
    private readonly TransportStream _cstream;
    private readonly CancellationTokenSource _ctokens;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    private bool _disposed;
    private byte[] _encryptionKey;
    private string? _remoteEndPoint;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Connection"/> class with a socket, buffer allocator, and optional logger.
    /// </summary>
    /// <param name="socket">The socket used for the connection.</param>
    /// <param name="bufferAllocator">The buffer pool used for data allocation.</param>
    /// <param name="logger">The logger used for logging connection events. If null, no logging will occur.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="socket"/> is null.</exception>
    public Connection(Socket socket, IBufferPool bufferAllocator, ILogger? logger = null)
    {
        _lock = new Lock();
        _id = Base36Id.NewId(IdentifierType.Session);
        _ctokens = new CancellationTokenSource();

        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _logger = logger;
        _cstream = new TransportStream(socket, bufferAllocator, _logger)
        {
            Disconnected = () =>
            {
                _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
            }
        };

        _cstream.SetPacketCached(() => _onProcessEvent?.Invoke(this, new ConnectionEventArgs(this)));

        _disposed = false;
        _encryptionKey = new byte[32];

        _logger?.Debug("[{0}] Connection created for {1}",
            nameof(Connection), _socket.RemoteEndPoint?.ToString());
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc />
    public IEncodedId Id => _id;

    /// <inheritdoc />
    public long UpTime => _cstream.UpTime;

    /// <inheritdoc />
    public long LastPingTime => _cstream.LastPingTime;

    /// <inheritdoc/>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <inheritdoc />
    public byte[] EncryptionKey
    {
        get => _encryptionKey;
        set
        {
            if (value is null || value.Length != 32)
                throw new ArgumentException("EncryptionKey must be exactly 16 bytes.", nameof(value));

            lock (_lock)
            {
                _encryptionKey = value;
            }
        }
    }

    /// <inheritdoc />
    public string RemoteEndPoint
    {
        get
        {
            if (_remoteEndPoint == null && _socket.Connected)
                _remoteEndPoint = _socket.RemoteEndPoint?.ToString() ?? "0.0.0.0";

            return _remoteEndPoint ?? "0.0.0.0";
        }
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> IncomingPacket => _cstream.GetIncomingPackets();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.Guest;

    /// <inheritdoc />
    public EncryptionType Encryption { get; set; } = EncryptionType.XTEA;

    /// <inheritdoc />
    public CompressionType Compression { get; set; } = CompressionType.Brotli;

    /// <inheritdoc />
    public AuthState State { get; set; } = AuthState.Connected;

    #endregion Properties

    #region Events

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnCloseEvent
    {
        add => _onCloseEvent += value;
        remove => _onCloseEvent -= value;
    }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnProcessEvent
    {
        add => _onProcessEvent += value;
        remove => _onProcessEvent -= value;
    }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnPostProcessEvent
    {
        add => _onPostProcessEvent += value;
        remove => _onPostProcessEvent -= value;
    }

    #endregion Events

    #region Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close(bool force = false)
    {
        try
        {
            if (!force && _socket.Connected &&
               (!_socket.Poll(1000, SelectMode.SelectRead) || _socket.Available > 0)) return;

            if (_disposed) return;

            this.State = AuthState.Disconnected;

            _ctokens.Cancel();
            _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error("[{0}] Close error: {1}", nameof(Connection), ex.Message);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disconnect(string? reason = null) => Close(force: true);

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
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
        catch (Exception ex)
        {
            _logger?.Error("[{0}] Dispose error: {1}", nameof(Connection), ex.Message);
        }
        finally
        {
            _ctokens.Dispose();
            _cstream.Dispose();
        }
    }

    #endregion Dispose Pattern
}
