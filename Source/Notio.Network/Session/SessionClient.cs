using Notio.Common.IMemory;
using Notio.Common.Models;
using Notio.Infrastructure.Identification;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System;

namespace Notio.Network.Session;

/// <summary>
/// Khởi tạo một đối tượng SessionClient.
/// </summary>
/// <param name="socket">Socket để kết nối.</param>
/// <param name="multiSizeBuffer">Bộ đệm đa kích thước.</param>
/// <param name="token">Token hủy bỏ.</param>
public sealed class SessionClient(Socket socket, IBufferAllocator multiSizeBuffer, CancellationToken token) : IDisposable
{
    private bool _isDisposed = false;

    private readonly UniqueId _id = UniqueId.NewId(TypeId.Session);
    private readonly CancellationToken _token = token;
    private readonly SessionNetwork _network = new(socket, multiSizeBuffer);
    private readonly SessionConnection _connection = new(socket, TimeSpan.FromSeconds(30));

    /// <summary>
    /// ID duy nhất của phiên làm việc.
    /// </summary>
    public UniqueId Id => _id;

    /// <summary>
    /// Vai trò của phiên làm việc.
    /// </summary>
    public Authoritys Role { get; private set; } = Authoritys.Guests;

    /// <summary>
    /// Mạng phiên làm việc.
    /// </summary>
    public SessionNetwork Network => _network;

    /// <summary>
    /// Khóa phiên làm việc.
    /// </summary>
    public byte[] SessionKey { get; init; } = [];

    /// <summary>
    /// Trạng thái kết nối.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Điểm cuối kết nối.
    /// </summary>
    public string EndPoint => _connection.EndPoint;

    /// <summary>
    /// Sự kiện xảy ra khi có thông tin.
    /// </summary>
    public event Action<string>? TraceOccurred;

    /// <summary>
    /// Sự kiện xảy ra khi có lỗi.
    /// </summary>
    public event Action<string, Exception>? ErrorOccurred;

    /// <summary>
    /// Thiết lập vai trò mới cho phiên làm việc.
    /// </summary>
    /// <param name="newRole">Vai trò mới.</param>
    public void SetRole(Authoritys newRole) => Role = newRole;

    /// <summary>
    /// Cập nhật thời gian hoạt động cuối cùng.
    /// </summary>
    public void UpdateLastActivityTime() => _connection.UpdateLastActivity();

    /// <summary>
    /// Kiểm tra xem phiên làm việc có bị hết hạn hay không.
    /// </summary>
    /// <returns>Trả về true nếu phiên làm việc bị hết hạn.</returns>
    public bool IsSessionTimedOut() => _connection.IsTimedOut();

    /// <summary>
    /// Kiểm tra xem socket có hợp lệ hay không.
    /// </summary>
    /// <returns>Trả về true nếu socket không hợp lệ.</returns>
    public bool IsSocketInvalid() => _isDisposed || _network.IsDispose;

    /// <summary>
    /// Kết nối phiên làm việc.
    /// </summary>
    public void Connect()
    {
        if (_isDisposed) return;

        try
        {
            IsConnected = true;
            ValidateConnection();

            _network.SocketReader.BeginReceiving(_token);
            TraceOccurred?.Invoke($"Session {_id} connected to {_connection.EndPoint}");
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            HandleConnectionError(ex, "Connection error");
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex, "Unexpected error");
        }
    }

    /// <summary>
    /// Kết nối lại phiên làm việc.
    /// </summary>
    public void Reconnect()
    {
        if (IsConnected || _isDisposed) return;

        int retries = 3;
        TimeSpan delay = TimeSpan.FromSeconds(2);

        while (retries > 0)
        {
            try
            {
                TraceOccurred?.Invoke("Attempting to reconnect...");
                Connect();
                return;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Reconnect attempt failed", ex);
                retries--;
                if (retries > 0)
                {
                    Thread.Sleep(delay);
                    delay = delay.Add(delay);
                }
            }
        }
    }

    /// <summary>
    /// Ngắt kết nối phiên làm việc.
    /// </summary>
    public void Disconnect()
    {
        if (!IsConnected || _isDisposed) return;

        IsConnected = false;
        try
        {
            _network.SocketReader?.CancelReceiving();
            Dispose();
            TraceOccurred?.Invoke($"Session {_id} disconnected from {_connection.EndPoint}");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Error during disconnect", ex);
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên và hủy đối tượng.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _network.Dispose();
        _connection.Dispose();
        _isDisposed = true;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của kết nối.
    /// </summary>
    private void ValidateConnection()
    {
        if (string.IsNullOrEmpty(_connection.EndPoint) || IsSocketInvalid())
        {
            TraceOccurred?.Invoke("Client address is invalid or Socket is not connected.");
            Disconnect();
        }
    }

    /// <summary>
    /// Xử lý lỗi kết nối.
    /// </summary>
    /// <param name="ex">Ngoại lệ xảy ra.</param>
    /// <param name="message">Thông báo lỗi.</param>
    private void HandleConnectionError(Exception ex, string message)
    {
        ErrorOccurred?.Invoke($"{message} for {_connection.EndPoint}", ex);
        Disconnect();
    }
}