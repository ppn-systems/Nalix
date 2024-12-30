using Notio.Common.Connection.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Common.Connection;

/// <summary>
/// Giao diện quản lý kết nối.
/// </summary>
public interface IConnection : IDisposable
{
    /// <summary>
    /// Khóa mã hóa tin nhắn
    /// </summary>
    byte[] EncryptionKey { get; }

    /// <summary>
    /// Trạng thái kết nối
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// Địa chỉ kết nối
    /// </summary>
    string RemoteEndPoint { get; }

    /// <summary>
    /// Thời điểm kết nối
    /// </summary>
    DateTimeOffset ConnectedTimestamp { get; }

    /// <summary>
    /// Sự kiện nhận dữ liệu.
    /// </summary>
    event EventHandler<ConnectionReceiveEventArgs> OnReceiveEvent;

    /// <summary>
    /// Sự kiện thay đổi trạng thái kết nối
    /// </summary>
    event EventHandler<ConnectionStateEventArgs> OnStateEvent;

    /// <summary>
    /// Sự kiện thông báo lỗi kết nối.
    /// </summary>
    event EventHandler<ConnectionErrorEventArgs> OnErrorEvent;

    /// <summary>
    /// Sự kiện xử lý kết nối.
    /// </summary>
    event EventHandler<IConnectionEventArgs> OnProcessEvent;

    /// <summary>
    /// Sự kiện ngắt kết nối.
    /// </summary>
    event EventHandler<IConnectionEventArgs> OnCloseEvent;

    /// <summary>
    /// Sự kiện xử lý kết nối sau khi ngắt.
    /// </summary>
    event EventHandler<IConnectionEventArgs> OnPostProcessEvent;

    /// <summary>
    /// Bắt đầu lắng nghe tin nhắn
    /// </summary>
    void BeginReceive(CancellationToken cancellationToken = default);

    // <summary>
    /// Kết thúc lắng nghe tin nhắn
    /// </summary>
    void CloseReceive();

    /// <summary>
    /// Gửi tin nhắn
    /// </summary>
    void Send(byte[] message);

    /// <summary>
    /// Gửi tin nhắn
    /// </summary>
    Task SendAsync(byte[] message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ngắt kết nối
    /// </summary>
    void Disconnect(string reason = null);
}