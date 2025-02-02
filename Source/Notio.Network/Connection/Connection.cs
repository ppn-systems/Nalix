using Notio.Common.Connection;
using Notio.Common.Connection.Enums;
using Notio.Common.Logging.Interfaces;
using Notio.Common.Memory.Pools;
using Notio.Common.Models;
using Notio.Shared.Identification;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

public sealed class Connection : IConnection, IDisposable
{
    private readonly Socket _socket;
    private readonly ILogger? _logger;
    private readonly ConnectionStreamHandler _streamHandler;
    private readonly CancellationTokenSource _ctokens = new();
    private readonly ConnectionStateManager _stateManager = new();
    private readonly UniqueId _id = UniqueId.NewId(TypeId.Session);
    private readonly ConnectionSecurityManager _securityManager = new();
    private readonly DateTimeOffset _connectedTimestamp = DateTimeOffset.UtcNow;

    private bool _disposed;

    public Connection(Socket socket, IBufferPool bufferAllocator, ILogger? logger = null)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _logger = logger;
        _streamHandler = new ConnectionStreamHandler(socket, bufferAllocator, logger);

        _streamHandler.OnDataReceived += OnDataReceived;
        _streamHandler.OnNewPacketCached = () =>
        {
            OnProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
        };
    }

    /// <inheritdoc />
    public string Id => _id.ToString(true);

    /// <inheritdoc />
    public ConnectionState State => _stateManager.State;

    /// <inheritdoc />
    public Authoritys Authority => _stateManager.Authority;

    /// <inheritdoc />
    public DateTimeOffset Timestamp => _connectedTimestamp;

    /// <inheritdoc />
    public long LastPingTime => _streamHandler.LastPingTime;

    /// <inheritdoc />
    public byte[] EncryptionKey => _securityManager.EncryptionKey;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnCloseEvent;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnProcessEvent;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnPostProcessEvent;

    /// <inheritdoc />
    public string RemoteEndPoint
        => _socket.Connected ? _socket.RemoteEndPoint?.ToString() ?? "0.0.0.0" : "Disconnected";

    /// <inheritdoc />
    public byte[]? IncomingPacket
    {
        get
        {
            if (_streamHandler.CacheIncomingPacket.TryGetValue(out var data))
                return data;
            return null;
        }
    }

    /// <inheritdoc />
    public void BeginReceive(CancellationToken cancellationToken = default)
        => _streamHandler.BeginReceive(cancellationToken);

    /// <inheritdoc />
    public void Close()
    {
        try
        {
            if (_socket.Connected && (!_socket.Poll(1000, SelectMode.SelectRead) || _socket.Available > 0))
            {
                return;
            }

            OnCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    /// <inheritdoc />
    public void Disconnect(string? reason = null)
    {
        try
        {
            if (_disposed) return;

            _ctokens.Cancel();
            _stateManager.UpdateState(ConnectionState.Disconnected);
            OnCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

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
            _streamHandler.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public void Send(ReadOnlySpan<byte> message)
    {
        if (_stateManager.State == ConnectionState.Authenticated)
            message = _securityManager.Encrypt(message.ToArray());

        if (_streamHandler.Send(message))
            OnPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
    }

    public async Task SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        if (_stateManager.State == ConnectionState.Authenticated)
            message = _securityManager.Encrypt(message).ToArray();

        if (await _streamHandler.SendAsync(message, cancellationToken))
            OnPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
    }

    private byte[] OnDataReceived(ReadOnlyMemory<byte> data)
    {
        if (_stateManager.State == ConnectionState.Authenticated)
            return _securityManager.Encrypt(data).ToArray();

        return data.ToArray();
    }
}