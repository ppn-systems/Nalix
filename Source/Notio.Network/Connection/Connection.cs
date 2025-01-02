using Notio.Common.IMemory;
using Notio.Common.INetwork;
using Notio.Common.INetwork.Args;
using Notio.Common.INetwork.Enums;
using Notio.Infrastructure.Identification;
using Notio.Infrastructure.Time;
using Notio.Network.Connection.Args;
using Notio.Security;
using Notio.Shared.Memory.Cache;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

public class Connection : IConnection, IDisposable
{
    private const byte HEADER_LENGHT = 2;
    private const short KEY_RSA_SIZE = 4096;
    private const short KEY_RSA_SIZE_BYTES = KEY_RSA_SIZE / 8;

    private readonly UniqueId _id;
    private readonly Socket _socket;
    private readonly Lock _receiveLock;
    private readonly NetworkStream _stream;
    private readonly BinaryCache _cacheOutgoingPacket;
    private readonly FifoCache<byte[]> _cacheIncomingPacket;
    private readonly ReaderWriterLockSlim _rwLockState;
    private readonly IBufferAllocator _bufferAllocator;
    private readonly DateTimeOffset _connectedTimestamp;

    private byte[] _buffer;
    private bool _disposed;
    private long _lastPingTime;
    private byte[] _aes256Key;
    private Rsa4096? _rsa4096;
    private ConnectionState _state;
    private CancellationTokenSource _ctokens;

    /// <summary>
    /// Khởi tạo một đối tượng INetwork mới.
    /// </summary>
    /// <param name="socket">Socket kết nối.</param>
    /// <param name="bufferAllocator">Bộ cấp phát bộ nhớ đệm.</param>
    public Connection(Socket socket, IBufferAllocator bufferAllocator)
    {
        _socket = socket;
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

    /// <summary>
    /// Thời gian kết nối.
    /// </summary>
    public DateTimeOffset Timestamp => _connectedTimestamp;

    /// <summary>
    /// Khóa mã hóa AES 256.
    /// </summary>
    public byte[] EncryptionKey => _aes256Key;

    /// <summary>
    /// Thời gian ping cuối cùng.
    /// </summary>
    public long LastPingTime => _lastPingTime;

    public string Id => _id.ToString(true);

    /// <summary>
    /// Điểm cuối từ xa của kết nối.
    /// </summary>
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

    /// <summary>
    /// Trạng thái kết nối.
    /// </summary>
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

    public event EventHandler<IErrorEventArgs>? OnErrorEvent;

    public event EventHandler<IConnctEventArgs>? OnProcessEvent;

    public event EventHandler<IConnctEventArgs>? OnCloseEvent;

    public event EventHandler<IConnctEventArgs>? OnPostProcessEvent;

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
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.StreamClosed, ex.Message));
            return;
        }
        catch (Exception ex)
        {
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.ReadError, ex.Message));
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
            // Log lỗi.
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.CloseError, ex.Message));
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
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.CloseError, ex.Message));
        }
        finally
        {
            _state = ConnectionState.Disconnected;
        }
    }

    public void Send(byte[] data)
    {
        try
        {
            if (_state == ConnectionState.Authenticated)
                data = Aes256.Encrypt(data, _aes256Key);

            if (!_cacheOutgoingPacket.TryGetValue(data, out byte[]? cachedData))
                _cacheOutgoingPacket.Add(data, data);

            // Gửi dữ liệu qua stream
            _stream.Write(data);
        }
        catch (Exception ex)
        {
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.SendError, ex.Message));
        }
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_state == ConnectionState.Authenticated)
                data = await Aes256.EncryptAsync(data, _aes256Key);

            if (!_cacheOutgoingPacket.TryGetValue(data, out byte[]? cachedData))
                _cacheOutgoingPacket.Add(data, data);

            // Gửi dữ liệu qua stream
            await _stream.WriteAsync(data, cancellationToken);
        }
        catch (Exception ex)
        {
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.SendError, ex.Message));
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
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.CloseError, ex.Message));
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
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.CloseError, ex.Message));
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
                OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.DataTooLarge,
                    $"Data length ({size} bytes) exceeds the maximum allowed buffer size ({_bufferAllocator.MaxBufferSize} bytes)."));
                return;
            }

            if (size > _buffer.Length)
                this.ResizeBuffer(size);

            while (totalBytesRead < size)
            {
                if (!_stream.CanRead) return;

                var bytesRead = await _stream.ReadAsync(
                    _buffer.AsMemory(totalBytesRead, size - totalBytesRead), _ctokens.Token);

                if (bytesRead == 0) break;

                totalBytesRead += bytesRead;
            }

            if (!_ctokens.Token.IsCancellationRequested)
            {
                _lastPingTime = (long)Clock.UnixTime.TotalMilliseconds;

                switch (_state)
                {
                    case ConnectionState.Connecting:
                        break;

                    case ConnectionState.Connected:
                        try
                        {
                            if (size < KEY_RSA_SIZE_BYTES) break;

                            _rsa4096 = new Rsa4096(KEY_RSA_SIZE);
                            _aes256Key = Aes256.GenerateKey();

                            _rsa4096.ImportPublicKey(_buffer
                                    .Skip(Math.Max(0, totalBytesRead - KEY_RSA_SIZE_BYTES))
                                    .Take(KEY_RSA_SIZE_BYTES)
                                    .ToArray()
                            );

                            byte[] key = _rsa4096.Encrypt(_aes256Key);
                            await _stream.WriteAsync(key, _ctokens.Token);

                            this.UpdateState(ConnectionState.Authenticated);
                        }
                        catch (Exception ex)
                        {
                            if (State == ConnectionState.Authenticated)
                                this.UpdateState(ConnectionState.Connected);

                            this.OnErrorEvent?.Invoke(this,
                                new ConnectionErrorEventArgs(ConnectionError.AuthenticationError, ex.Message));
                        }
                        break;

                    case ConnectionState.Authenticated:
                        try
                        {
                            byte[] decrypted = await Aes256.DecryptAsync(
                                _aes256Key, _buffer.Take(totalBytesRead).ToArray());

                            _cacheIncomingPacket.Add(decrypted);
                            this.OnProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
                        }
                        catch (CryptographicException ex)
                        {
                            this.UpdateState(ConnectionState.Connecting);

                            this.OnErrorEvent?.Invoke(
                                this, new ConnectionErrorEventArgs(ConnectionError.DecryptionError, ex.Message));
                        }
                        catch (Exception ex)
                        {
                            this.OnErrorEvent?.Invoke(
                                this, new ConnectionErrorEventArgs(ConnectionError.DecryptionError, ex.Message));
                        }
                        break;

                    default:
                        _cacheIncomingPacket.Add(_buffer.Take(totalBytesRead).ToArray());
                        this.OnProcessEvent?.Invoke(this, new ConnectionEventArgs(this));
                        break;
                }
            }

            this.BeginReceive();
        }
        catch (Exception ex)
        {
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.ReadError, ex.Message));
        }
    }
}