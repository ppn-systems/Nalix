using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Notio.Network.Session;

/// <summary>
/// Quản lý kết nối socket của khách hàng và theo dõi thời gian hoạt động.
/// </summary>
internal sealed class SessionConnection(Socket socket, TimeSpan timeout)
{
    private readonly Socket _socket = socket ?? throw new ArgumentNullException(nameof(socket));
    private readonly Stopwatch _activityTimer = Stopwatch.StartNew();
    private readonly string _clientIp = socket.RemoteEndPoint is IPEndPoint remoteEndPoint ? remoteEndPoint.Address.ToString() : string.Empty;

    private TimeSpan _timeout = timeout;

    /// <summary>
    /// Kiểm tra trạng thái kết nối của phiên làm việc.
    /// </summary>
    public bool IsConnected => _socket.Connected;

    /// <summary>
    /// Địa chỉ IP của khách hàng.
    /// </summary>
    public string EndPoint => _clientIp;

    /// <summary>
    /// Cập nhật thời gian hoạt động cuối cùng của kết nối.
    /// </summary>
    public void UpdateLastActivity() => _activityTimer.Restart();

    /// <summary>
    /// Thiết lập thời gian chờ cho kết nối.
    /// </summary>
    /// <param name="timeout">Thời gian chờ.</param>
    public void SetTimeout(TimeSpan timeout)
    { _timeout = timeout; }

    /// <summary>
    /// Kiểm tra xem phiên làm việc có hết thời gian chờ không.
    /// </summary>
    /// <returns>True nếu phiên làm việc đã hết thời gian chờ, ngược lại False.</returns>
    public bool IsTimedOut() => _activityTimer.Elapsed > _timeout;

    /// <summary>
    /// Giải phóng tài nguyên của kết nối.
    /// </summary>
    public void Dispose()
    {
        _socket?.Close();
        _socket?.Dispose();
    }
}