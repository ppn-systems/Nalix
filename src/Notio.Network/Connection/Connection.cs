using Notio.Common.Caching;
using Notio.Common.Connection;
using Notio.Common.Identity;
using Notio.Common.Logging;
using Notio.Common.Package;
using Notio.Common.Security;
using Notio.Identifiers;
using Notio.Network.Connection.Transport;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed class Connection : IConnection
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

    #endregion

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
        _id = Base36Id.NewId(IdType.Session);
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

    #endregion

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
    public ReadOnlyMemory<byte> IncomingPacket
        => _cstream.GetIncomingPackets();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.Guest;

    /// <inheritdoc />
    public EncryptionMode EncMode { get; set; } = EncryptionMode.XTEA;

    /// <inheritdoc />
    public CompressionMode ComMode { get; set; } = CompressionMode.Brotli;

    /// <inheritdoc />
    public ConnectionState State { get; set; } = ConnectionState.Connected;

    #endregion

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

    #endregion

    #region Methods

    /// <inheritdoc />
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(Connection));

        using CancellationTokenSource linkedCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _ctokens.Token);

        _cstream.BeginReceive(linkedCts.Token);
    }

    /// <inheritdoc />
    public bool Send(IPacket packet) => Send(packet.Serialize().Span);

    /// <inheritdoc />
    public bool Send(ReadOnlySpan<byte> message)
    {
        bool success = _cstream.Send(message);
        if (success)
            _onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
        else
            _logger?.Warn($"[{nameof(Connection)}] Failed to send message.");
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(IPacket packet, CancellationToken cancellationToken = default)
        => await SendAsync(packet.Serialize(), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (await _cstream.SendAsync(message, cancellationToken))
        {
            _onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
            return true;
        }

        _logger?.Warn($"[{nameof(Connection)}] Failed to send message asynchronously.");
        return false;
    }

    /// <inheritdoc />
    public void Close(bool force = false)
    {
        try
        {
            if (!force && _socket.Connected && (!_socket.Poll(1000, SelectMode.SelectRead) || _socket.Available > 0))
            {
                return;
            }

            if (_disposed)
                return;

            State = ConnectionState.Disconnected;

            _ctokens.Cancel();
            _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error("[{0}] Close error: {1}", nameof(Connection), ex.Message);
        }
    }

    /// <inheritdoc />
    public void Disconnect(string? reason = null) => Close(force: true);

    #endregion

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
            Disconnect();
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

    #endregion
}
