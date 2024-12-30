using Notio.Common.Connection;
using Notio.Common.Connection.Enums;
using Notio.Common.IMemory;
using Notio.Infrastructure.Services;
using Notio.Infrastructure.Time;
using Notio.Logging;
using Notio.Security;
using Notio.Shared.Memory;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

public class Connection : IConnection, IDisposable
{
    private const ushort MaxSizeBuffer = 16384;

    private readonly UniqueId _id;
    private readonly Socket _socket;
    private readonly LRUCache _cache;
    private readonly NetworkStream _stream;
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
    /// Khởi tạo một đối tượng Connection mới.
    /// </summary>
    /// <param name="socket">Socket kết nối.</param>
    /// <param name="bufferAllocator">Bộ cấp phát bộ nhớ đệm.</param>
    public Connection(Socket socket, IBufferAllocator bufferAllocator)
    { 
        _socket = socket;
        _cache = new LRUCache(20);
        _stream = new NetworkStream(socket);
        _bufferAllocator = bufferAllocator;
        _id = UniqueId.NewId(TypeId.Session);
        _ctokens = new CancellationTokenSource();
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
    public DateTimeOffset ConnectedTimestamp => _connectedTimestamp;

    /// <summary>
    /// Khóa mã hóa AES 256.
    /// </summary>
    public byte[] EncryptionKey => _aes256Key;

    /// <summary>
    /// Thời gian ping cuối cùng.
    /// </summary>
    public long LastPingTime => _lastPingTime;

    /// <summary>
    /// Trạng thái kết nối.
    /// </summary>
    public ConnectionState State => _state;

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

    public string Id => _id.ToHex();

    public event EventHandler<ConnectionStateEventArgs>? OnStateEvent;
    public event EventHandler<ConnectionErrorEventArgs>? OnErrorEvent;
    public event EventHandler<ConnectionReceiveEventArgs>? OnReceiveEvent;

    public event EventHandler<IConnectionEventArgs>? OnProcessEvent;
    public event EventHandler<IConnectionEventArgs>? OnCloseEvent;
    public event EventHandler<IConnectionEventArgs>? OnPostProcessEvent;

    /// <summary>
    /// Bắt đầu nhận dữ liệu không đồng bộ.
    /// </summary>
    /// <param name="cancellationToken">Token hủy bỏ.</param>
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        if (cancellationToken != default)
        {
            _ctokens.Dispose();
            _ctokens = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            _stream.BeginRead(
                _buffer, 0, _buffer.Length,
                new AsyncCallback(OnReceiveCompleted), (_buffer, _ctokens.Token));
        }
        catch (Exception ex)
        {
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.ReadError, ex.Message));
        }
    }

    /// <summary>
    /// Đóng kết nối nhận dữ liệu.
    /// </summary>
    public void CloseReceive()
    {
        try
        {
            //todo needs to remove this connection from pool
            if (!_socket.Connected)
            {
                if (_stream.CanRead) _stream.Close();
                return;
            }

            // Tells the subscribers of this event that this connection has been closed.
            OnCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        catch (Exception ex)
        {
            NotioLog.Instance.Error("Unable to close socket connection", ex);
        }
    }

    /// <summary>
    /// Kiểm tra và điều chỉnh bộ đệm nếu kích thước dữ liệu nhận được lớn hơn kích thước bộ đệm hiện tại.
    /// </summary>
    /// <param name="newSize">Kích thước buffer mới.</param>
    /// <returns>Trả về `true` nếu bộ đệm được thay đổi, `false` nếu không thay đổi.</returns>
    public bool ResizeBuffer(int newSize)
    {
        if (newSize > _buffer.Length)
        {
            byte[] newBuffer = _bufferAllocator.Rent(newSize);
            Array.Copy(_buffer, newBuffer, _buffer.Length);
            _bufferAllocator.Return(_buffer);
            _buffer = newBuffer;

            return true;
        }

        return false;
    }

    public void Disconnect(string? reason = null)
    {
        try
        {
            _ctokens.Cancel();  // Hủy bỏ token khi kết nối đang chờ hủy
            _stream.Close();    // Đóng luồng mạng
            _socket.Close();    // Đóng socket
        }
        catch (Exception ex)
        {
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.CloseError, ex.Message));
        }
        finally
        {
            OnStateEvent?.Invoke(this, new ConnectionStateEventArgs(_state, ConnectionState.Disconnected));  // Gửi sự kiện đóng kết nối
            _state = ConnectionState.Disconnected;
        }
    }

    public void Send(byte[] data)
    {
        try
        {
            if (_state == ConnectionState.Authenticated)
                data = Aes256.Encrypt(data, _aes256Key);

            if (!_cache.TryGetValue(data, out byte[]? cachedData))
                _cache.Add(data, data);

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

            if (!_cache.TryGetValue(data, out byte[]? cachedData))
                _cache.Add(data, data);

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
            OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.Undefined, ex.Message));
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

    private async void OnReceiveCompleted(IAsyncResult ar)
    {
        if (ar.AsyncState is Tuple<byte[], CancellationToken> state)
        {
            byte[] buffer = state.Item1;
            CancellationToken cancellationToken = state.Item2;

            try
            {
                if (!_socket.Connected || !_stream.CanRead) return;

                int bytesRead = _stream.EndRead(ar);
                ushort dataLength = BitConverter.ToUInt16(buffer, 0);
                if (dataLength > MaxSizeBuffer)
                {
                    OnErrorEvent?.Invoke(this,
                        new ConnectionErrorEventArgs(ConnectionError.DataTooLarge,
                        $"Data length ({dataLength} bytes) " +
                        $"exceeds the maximum allowed buffer size ({MaxSizeBuffer} bytes)."));
                }
                else if (bytesRead == 0 && dataLength != bytesRead)
                {
                    OnErrorEvent?.Invoke(this,
                        new ConnectionErrorEventArgs(ConnectionError.DataMismatch, "Data length mismatch detected."));
                }
                else if (!cancellationToken.IsCancellationRequested)
                {
                    _lastPingTime = (long)Clock.UnixTime.TotalMilliseconds;

                    switch (_state)
                    {
                        case ConnectionState.Connecting:
                            break;

                        case ConnectionState.Connected:
                            // Xác thực kết nối
                            try
                            {
                                if (!(dataLength is >= 500 and <= 600)) { break; }

                                _rsa4096 = new Rsa4096(4092);
                                _aes256Key = Aes256.GenerateKey();
                                _rsa4096.ImportPublicKey(buffer.Skip(Math.Max(0, bytesRead - 512)).Take(512).ToArray());

                                byte[] key = _rsa4096.Encrypt(_aes256Key);

                                await _stream.WriteAsync(key, cancellationToken);

                                _state = ConnectionState.Authenticated;

                                OnStateEvent?.Invoke(
                                    this, new ConnectionStateEventArgs(ConnectionState.Connecting, ConnectionState.Authenticated));
                            }
                            catch (Exception ex)
                            {
                                OnErrorEvent?.Invoke(
                                    this, new ConnectionErrorEventArgs(ConnectionError.AuthenticationError, ex.Message));
                            }
                            break;

                        case ConnectionState.Authenticated:
                            // Giải mã dữ liệu
                            try
                            {
                                byte[] decrypted = await Aes256.DecryptAsync(
                                    _aes256Key, buffer.Take(bytesRead).ToArray());

                                OnReceiveEvent?.Invoke(this, new ConnectionReceiveEventArgs(decrypted));
                            }
                            catch (Exception ex)
                            {
                                OnErrorEvent?.Invoke(
                                    this, new ConnectionErrorEventArgs(ConnectionError.EncryptionError, ex.Message));
                            }
                            break;

                        default:
                            OnReceiveEvent?.Invoke(this, new ConnectionReceiveEventArgs(buffer.Take(bytesRead).ToArray()));
                            break;
                    }
                }
                else
                {
                    await Task.Yield();
                }

                this.BeginReceive(); // Gọi lại để nhận dữ liệu tiếp theo
            }
            catch (Exception ex)
            {
                OnErrorEvent?.Invoke(this, new ConnectionErrorEventArgs(ConnectionError.ReadError, ex.Message));
            }
        }
    }
}