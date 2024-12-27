using Notio.Network.Config;
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Session;

/// <summary>
/// Quản lý phiên làm việc cho một kết nối socket.
/// </summary>
public class SocketSession : IDisposable
{
    private readonly Socket _socket;
    private readonly NetworkStream _networkStream;
    private readonly SslStream? _sslStream;
    private readonly byte[] _receiveBuffer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    /// <summary>
    /// ID của phiên làm việc.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Điểm cuối từ xa của kết nối socket.
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Cho biết liệu kết nối có còn hoạt động hay không.
    /// </summary>
    public bool IsConnected => _socket.Connected;

    /// <summary>
    /// Cho biết liệu kết nối có bảo mật hay không.
    /// </summary>
    public bool IsSecure => _sslStream != null;

    /// <summary>
    /// Sự kiện được kích hoạt khi dữ liệu được nhận từ khách hàng.
    /// </summary>
    public event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// Sự kiện được kích hoạt khi kết nối bị ngắt.
    /// </summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// Sự kiện được kích hoạt khi xảy ra lỗi.
    /// </summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Khởi tạo một <see cref="SocketSession"/> mới.
    /// </summary>
    /// <param name="socket">Socket cho phiên làm việc.</param>
    /// <param name="bufferSize">Kích thước bộ đệm nhận.</param>
    /// <param name="security">Cấu hình bảo mật (tùy chọn).</param>
    public SocketSession(Socket socket, int bufferSize, SecurityConfig? security = null)
    {
        _socket = socket;
        Id = Guid.NewGuid().ToString();
        RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
        _receiveBuffer = new byte[bufferSize];
        _networkStream = new NetworkStream(socket);

        if (security?.ServerCertificate != null)
        {
            _sslStream = new SslStream(_networkStream, false);
            ConfigureSslAsync(security).Wait();
        }
    }

    private async Task ConfigureSslAsync(SecurityConfig security)
    {
        await _sslStream!.AuthenticateAsServerAsync(
            security.ServerCertificate!,
            security.RequireClientCertificate,
            security.EnabledProtocols,
            security.CheckCertificateRevocation);
    }

    /// <summary>
    /// Bắt đầu nhận dữ liệu từ khách hàng.
    /// </summary>
    public async Task StartReceiving()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && IsConnected)
            {
                int bytesRead;
                if (_sslStream != null)
                    bytesRead = await _sslStream.ReadAsync(_receiveBuffer);
                else
                    bytesRead = await _networkStream.ReadAsync(_receiveBuffer);

                if (bytesRead == 0)
                {
                    Disconnect();
                    break;
                }

                var data = new byte[bytesRead];
                Array.Copy(_receiveBuffer, data, bytesRead);
                DataReceived?.Invoke(this, data);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            Disconnect();
        }
    }

    /// <summary>
    /// Gửi dữ liệu đến khách hàng.
    /// </summary>
    /// <param name="data">Dữ liệu để gửi.</param>
    public async Task SendAsync(byte[] data)
    {
        try
        {
            if (_sslStream != null)
                await _sslStream.WriteAsync(data);
            else
                await _networkStream.WriteAsync(data);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            Disconnect();
        }
    }

    /// <summary>
    /// Ngắt kết nối từ khách hàng.
    /// </summary>
    public void Disconnect()
    {
        if (!IsConnected) return;

        try
        {
            _cancellationTokenSource.Cancel();
            _socket.Shutdown(SocketShutdown.Both);
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            _socket.Close();
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi <see cref="SocketSession"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi <see cref="SocketSession"/>.
    /// </summary>
    /// <param name="disposing">True để giải phóng tài nguyên quản lý, False để chỉ giải phóng tài nguyên không quản lý.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                _sslStream?.Dispose();
                _networkStream.Dispose();
                _socket.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên khi hủy đối tượng.
    /// </summary>
    ~SocketSession()
    {
        Dispose(false);
    }
}