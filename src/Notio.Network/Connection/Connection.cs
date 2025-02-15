using Notio.Common.Connection;
using Notio.Common.Connection.Enums;
using Notio.Common.Enums;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Common.Models;
using Notio.Identification;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

/// <summary>
/// Represents a network connection that manages socket communication, stream transformation, and event handling.
/// </summary>
public sealed class Connection : IConnection, IDisposable
{
    private readonly Socket _socket;
    private readonly ILogger? _logger;
    private readonly ConnectionStream _cstream;
    private readonly CancellationTokenSource _ctokens = new();
    private readonly UniqueId _id = UniqueId.NewId(TypeId.Session);
    private readonly DateTimeOffset _connectedTimestamp = DateTimeOffset.UtcNow;

    private bool _disposed;

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
                OnProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
            }
        };
    }

    /// <inheritdoc />
    public string Id => _id.ToString(true);

    /// <inheritdoc />
    public byte[] EncryptionKey { get; set; } = [];

    /// <inheritdoc />
    public EncryptionMode Mode { get; set; } = EncryptionMode.Xtea;

    /// <inheritdoc />
    public long PingTime => _cstream.LastPingTime;

    /// <inheritdoc />
    public DateTimeOffset Timestamp => _connectedTimestamp;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnCloseEvent;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnProcessEvent;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnPostProcessEvent;

    /// <inheritdoc />
    public Authoritys Authority { get; set; } = Authoritys.Guests;

    /// <inheritdoc />
    public ConnectionState State { get; set; } = ConnectionState.Connected;

    /// <inheritdoc />
    public string RemoteEndPoint
        => _socket.Connected ? _socket.RemoteEndPoint?.ToString() ?? "0.0.0.0" : "Disconnected";

    /// <inheritdoc />
    public ReadOnlyMemory<byte>? IncomingPacket
    {
        get
        {
            if (_cstream.CacheIncoming.TryGetValue(out ReadOnlyMemory<byte> data))
                return data;
            return null;
        }
    }

    /// <inheritdoc />
    public void BeginReceive(CancellationToken cancellationToken = default)
        => _cstream.BeginReceive(cancellationToken);

    /// <inheritdoc />
    public void Send(Memory<byte> message)
    {
        if (_cstream.Send(message))
            OnPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
    }

    /// <inheritdoc />
    public async Task SendAsync(Memory<byte> message, CancellationToken cancellationToken = default)
    {
        if (await _cstream.SendAsync(message, cancellationToken))
            OnPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
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
            OnCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    /// <inheritdoc />
    public void Disconnect(string? reason = null) => Close(force: true);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            Disconnect();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
        finally
        {
            _ctokens.Dispose();
            _cstream.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
