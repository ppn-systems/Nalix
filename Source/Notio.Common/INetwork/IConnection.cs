using Notio.Common.INetwork.Args;
using Notio.Common.INetwork.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Common.INetwork;

/// <summary>
/// Giao diện quản lý kết nối.
/// </summary>
public interface IConnection : IDisposable
{
    string Id { get; }
    byte[] IncomingPacket { get; }

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
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Sự kiện xử lý nhận dữ liệu.
    /// </summary>
    event EventHandler<IConnctEventArgs> OnProcessEvent;

    /// <summary>
    /// Sự kiện ngắt kết nối.
    /// </summary>
    event EventHandler<IConnctEventArgs> OnCloseEvent;

    /// <summary>
    /// Sự kiện sau khi xử lý dữ liệu xong.
    /// </summary>
    event EventHandler<IConnctEventArgs> OnPostProcessEvent;

    /// <summary>
    /// Sự kiện thông báo lỗi kết nối.
    /// </summary>
    event EventHandler<IErrorEventArgs> OnErrorEvent;

    /// <summary>
    /// Bắt đầu lắng nghe tin nhắn
    /// </summary>
    void BeginReceive(CancellationToken cancellationToken = default);

    // <summary>
    /// Kết thúc lắng nghe tin nhắn
    /// </summary>
    void Close();

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