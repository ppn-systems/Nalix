using Notio.Common.Connection;
using Notio.Common.Authentication;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Common.Models;
using Notio.Identification;
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

    private readonly Socket _socket;
    private readonly ILogger? _logger;
    private readonly Lock _lock = new();
    private readonly ConnectionStream _cstream;
    private readonly CancellationTokenSource _ctokens = new();
    private readonly UniqueId _id = UniqueId.NewId(TypeId.Session);

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    private bool _disposed = false;
    private string? _remoteEndPoint;
    private byte[] _encryptionKey = new byte[32];

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
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _logger = logger;
        _cstream = new ConnectionStream(socket, bufferAllocator, logger)
        {
            PacketCached = () =>
            {
                _onProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
            }
        };
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public string Id => _id.ToString(true);

    /// <inheritdoc />
    public long PingTime => _cstream.LastPingTime;

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

            return _remoteEndPoint ?? "Disconnected";
        }
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> IncomingPacket
    {
        get
        {
            if (_cstream.CacheIncoming.TryGetValue(out ReadOnlyMemory<byte> data))
                return data;

            return ReadOnlyMemory<byte>.Empty; // Avoid null
        }
    }

    /// <inheritdoc />
    public Authoritys Authority { get; set; } = Authoritys.Guest;

    /// <inheritdoc />
    public EncryptionMode Mode { get; set; } = EncryptionMode.Xtea;

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

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
        using CancellationTokenSource linkedCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _ctokens.Token);
        _cstream.BeginReceive(linkedCts.Token);
    }

    /// <inheritdoc />
    public bool Send(Memory<byte> message)
    {
        bool success = _cstream.Send(message);
        if (success)
            _onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
        else
            _logger?.Warn("Failed to send message.");
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(Memory<byte> message, CancellationToken cancellationToken = default)
    {
        bool success = await _cstream.SendAsync(message, cancellationToken);
        if (success)
            _onPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
        else
            _logger?.Warn("Failed to send message asynchronously.");
        return success;
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

            if (_disposed) return;

            _ctokens.Cancel();
            this.State = ConnectionState.Disconnected;
            _onCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
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
            if (_disposed) return;
            _disposed = true;
        }

        try
        {
            this.Disconnect();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
        finally
        {
            _ctokens.Dispose();
            _cstream.Dispose();
        }
    }

    #endregion
}
