using System;

namespace Notio.Common.Contracts.Network;

/// <summary>
/// Giao diện đại diện cho một kết nối mạng.
/// </summary>
public interface IConnection
{
    /// <summary>
    /// Khóa Aes cho kết nối.
    /// </summary>
    byte[] AesKey { get; }

    /// <summary>
    /// Trạng thái xác thực của kết nối.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Trạng thái ngắt kết nối.
    /// </summary>
    bool Disconnected { get; }

    /// <summary>
    /// Thời gian yêu cầu ping cuối cùng.
    /// </summary>
    long LastPingRequest { get; set; }

    /// <summary>
    /// Thời gian phản hồi ping cuối cùng.
    /// </summary>
    long LastPingResponse { get; set; }

    /// <summary>
    /// Địa chỉ IP của kết nối.
    /// </summary>
    string Ip { get; }

    /// <summary>
    /// Dấu thời gian của kết nối.
    /// </summary>
    long TimeStamp { get; }

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
    /// Bắt đầu đọc luồng dữ liệu.
    /// </summary>
    void BeginStreamRead();

    /// <summary>
    /// Đóng kết nối.
    /// </summary>
    /// <param name="force">Bắt buộc đóng kết nối.</param>
    void Close(bool force = false);

    /// <summary>
    /// Ngắt kết nối.
    /// </summary>
    /// <param name="text">Thông báo khi ngắt kết nối.</param>
    void Disconnect(string text = null);

    /// <summary>
    /// Gửi yêu cầu kết nối đầu tiên.
    /// </summary>
    void SendFirstConnection();

    /// <summary>
    /// Thiết lập khóa Aes.
    /// </summary>
    /// <param name="key">Khóa Aes.</param>
    void SetAes(byte[] key);

    /// <summary>
    /// Đánh dấu kết nối đã được xác thực.
    /// </summary>
    void SetAsAuthenticated();

    /// <summary>
    /// Gửi dữ liệu qua kết nối.
    /// </summary>
    /// <param name="data">Dữ liệu cần gửi.</param>
    void Send(byte[] data);
}