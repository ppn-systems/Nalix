using Notio.Common.Connection;
using Notio.Common.Connection.Args;
using Notio.Common.Connection.Enums;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Cryptography;
using Notio.Infrastructure.Identification;
using Notio.Infrastructure.Time;
using Notio.Shared.Memory.Cache;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

public sealed class Connection : IConnection, IDisposable
{
    private const byte HEADER_LENGHT = 2;
    private const short KEY_RSA_SIZE = 4096;
    private const short KEY_RSA_SIZE_BYTES = KEY_RSA_SIZE / 8;

    private readonly UniqueId _id;
    private readonly Socket _socket;
    private readonly ILogger? _logger;
    private readonly Lock _receiveLock;
    private readonly NetworkStream _stream;
    private readonly BinaryCache _cacheOutgoingPacket;
    private readonly ReaderWriterLockSlim _rwLockState;
    private readonly IBufferPool _bufferAllocator;
    private readonly DateTimeOffset _connectedTimestamp;
    private readonly FifoCache<byte[]> _cacheIncomingPacket;

    private byte[] _buffer;
    private bool _disposed;
    private long _lastPingTime;
    private byte[] _aes256Key;
    private Rsa4096? _rsa4096;
    private ConnectionState _state;
    private CancellationTokenSource _ctokens;

    /// <summary>
    /// Khởi tạo một đối tượng Connection mới.
    /// </summary>
    /// <param name="socket">Socket kết nối.</param>
    /// <param name="bufferAllocator">Bộ cấp phát bộ nhớ đệm.</param>
    public Connection(Socket socket, IBufferPool bufferAllocator, ILogger? logger = null)
    {
        _socket = socket;
        _logger = logger;
        _receiveLock = new Lock();
        _stream = new NetworkStream(socket);
        _bufferAllocator = bufferAllocator;
        _id = UniqueId.NewId(TypeId.Session);
        _cacheOutgoingPacket = new BinaryCache(20);
        _cacheIncomingPacket = new FifoCache<byte[]>(20);
        _ctokens = new CancellationTokenSource();
        _rwLockState = new ReaderWriterLockSlim();
        _connectedTimestamp = DateTimeOffset.UtcNow;

        _disposed = false;
        _aes256Key = [];
        _state = ConnectionState.Connecting;
        _buffer = _bufferAllocator.Rent(1024); // byte
        _lastPingTime = (long)Clock.UnixTime.TotalMilliseconds;
    }

    /// <inheritdoc />
    public string Id => _id.ToString(true);

    /// <inheritdoc />
    public byte[] EncryptionKey => _aes256Key;

    /// <inheritdoc />
    public long LastPingTime => _lastPingTime;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnCloseEvent;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnProcessEvent;

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs>? OnPostProcessEvent;

    /// <inheritdoc />
    public DateTimeOffset Timestamp => _connectedTimestamp;

    /// <inheritdoc />
    public string RemoteEndPoint
    {
        get
        {
            return
                (_socket?.Connected ?? false)
                ? _socket.RemoteEndPoint?.ToString()
                ?? "0.0.0.0" : "Disconnected";
        }
    }

    /// <inheritdoc />
    public byte[]? IncomingPacket
    {
        get
        {
            if (_cacheIncomingPacket.Count > 0)
                return _cacheIncomingPacket.GetValue();
            else 
                return null;
        }
    }

    /// <inheritdoc />
    public ConnectionState State
    {
        get
        {
            _rwLockState.EnterReadLock();
            try
            {
                return _state;
            }
            finally
            {
                _rwLockState.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Bắt đầu nhận dữ liệu không đồng bộ.
    /// </summary>
    /// <param name="cancellationToken">Token hủy bỏ.</param>
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        lock (_receiveLock)
        {
            if (!_socket.Connected || !_stream.CanRead) return;
        }

        if (cancellationToken != default)
        {
            _ctokens.Dispose();
            _ctokens = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            lock (_receiveLock)
            {
                _stream.ReadAsync(_buffer, 0, HEADER_LENGHT, _ctokens.Token)
                       .ContinueWith(OnReceiveCompleted, _ctokens.Token);
            }
        }
        catch (ObjectDisposedException ex)
        {
            _logger?.Error(ex);
            return;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    /// <summary>
    /// Đóng kết nối nhận dữ liệu.
    /// </summary>
    public void Close()
    {
        try
        {
            // TODO: Remove this connection from the pool.

            // Kiểm tra trạng thái kết nối thực tế của socket.
            if (_socket == null || !_socket.Connected || _socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0)
            {
                // Đảm bảo stream được đóng nếu socket không còn kết nối.
                _stream?.Close();

                // Thông báo trước khi đóng socket.
                OnCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    public void Disconnect(string? reason = null)
    {
        try
        {
            _ctokens.Cancel();  // Hủy bỏ token khi kết nối đang chờ hủy
            this.CloseSocket();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
        finally
        {
            this.UpdateState(ConnectionState.Disconnected);
        }
    }

    public void Send(ReadOnlySpan<byte> message)
    {
        try
        {
            if (_state == ConnectionState.Authenticated)
            {
                ReadOnlyMemory<byte> memoryBuffer = Aes256.GcmMode.Encrypt(message.ToArray(), _aes256Key);
                message = memoryBuffer.Span;
            }

            Span<byte> key = stackalloc byte[10];
            message[..4].CopyTo(key);
            message[(message.Length - 5)..].CopyTo(key);

            if (!_cacheOutgoingPacket.TryGetValue(key, out ReadOnlyMemory<byte>? cachedData))
                _cacheOutgoingPacket.Add(key, message.ToArray());

            _stream.Write(message);

            OnPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    public async Task SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_state == ConnectionState.Authenticated)
                message = Aes256.GcmMode.Encrypt(message, _aes256Key).ToArray();

            Span<byte> key = stackalloc byte[10];
            message.AsSpan(0, 4).CopyTo(key);
            message.AsSpan(message.Length - 5).CopyTo(key);

            if (!_cacheOutgoingPacket.TryGetValue(key, out ReadOnlyMemory<byte>? cachedData))
                _cacheOutgoingPacket.Add(key, message);

            await _stream.WriteAsync(message, cancellationToken);

            OnPostProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            this.Disconnect();

            _bufferAllocator.Return(_buffer);
            _buffer = [];
            _aes256Key = [];
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
        finally
        {
            _disposed = true;
            _ctokens.Dispose();
            _stream.Dispose();
            _socket.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private void CloseSocket()
    {
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    private void UpdateState(ConnectionState newState)
    {
        _rwLockState.EnterWriteLock();
        try
        {
            _state = newState;
        }
        finally
        {
            _rwLockState.ExitWriteLock();
        }
    }

    private void ResizeBuffer(int newSize)
    {
        byte[] newBuffer = _bufferAllocator.Rent(newSize);
        Array.Copy(_buffer, newBuffer, _buffer.Length);
        _bufferAllocator.Return(_buffer);
        _buffer = newBuffer;
    }

    private async Task OnReceiveCompleted(Task<int> task)
    {
        if (task.IsCanceled || _disposed) return;

        try
        {
            int totalBytesRead = task.Result;
            ushort size = BitConverter.ToUInt16(_buffer, 0);

            if (size > _bufferAllocator.MaxBufferSize)
            {
                _logger?.Error($"Data length ({size} bytes) " +
                    $"exceeds the maximum allowed buffer size ({_bufferAllocator.MaxBufferSize} bytes).");
                return;
            }

            if (size > _buffer.Length)
                this.ResizeBuffer(Math.Max(_buffer.Length * 2, size));

            while (totalBytesRead < size)
            {
                int bytesRead = await _stream.ReadAsync(_buffer.AsMemory(totalBytesRead, size - totalBytesRead), _ctokens.Token);
                if (bytesRead == 0) break;
                totalBytesRead += bytesRead;
            }

            if (!_ctokens.Token.IsCancellationRequested)
                _lastPingTime = (long)Clock.UnixTime.TotalMilliseconds;

            await this.HandleConnectionStateAsync(size, totalBytesRead);
            this.BeginReceive();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    private async Task HandleConnectionStateAsync(ushort size, int totalBytesRead)
    {
        switch (_state)
        {
            case ConnectionState.Connecting:
                break;

            case ConnectionState.Connected:
                await this.HandleConnectedState(totalBytesRead, size);
                break;

            case ConnectionState.Authenticated:
                this.HandleAuthenticatedState(totalBytesRead);
                break;

            default:
                break;
        }
    }

    private async Task HandleConnectedState(int totalBytesRead, ushort size)
    {
        try
        {
            if (size < KEY_RSA_SIZE_BYTES) return;

            _rsa4096 = new Rsa4096(KEY_RSA_SIZE);
            _aes256Key = Aes256.GenerateKey();
            _rsa4096.ImportPublicKey(_buffer
                .Skip(Math.Max(0, totalBytesRead - KEY_RSA_SIZE_BYTES))
                .Take(KEY_RSA_SIZE_BYTES).ToArray());

            byte[] key = _rsa4096.Encrypt(_aes256Key);
            await _stream.WriteAsync(key, _ctokens.Token);

            this.UpdateState(ConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
            this.UpdateState(ConnectionState.Connecting);
        }
    }

    private void HandleAuthenticatedState(int totalBytesRead)
    {
        try
        {
            _cacheIncomingPacket.Add(_buffer.Take(totalBytesRead).ToArray());
            this.OnProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
            this.UpdateState(ConnectionState.Connecting);
        }
    }
}