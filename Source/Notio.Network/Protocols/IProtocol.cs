using Notio.Common.Contracts.Network;

namespace Notio.Network.Protocols;

/// <summary>
/// Giao diện đại diện cho một giao thức mạng.
/// </summary>
public interface IProtocol
{
    /// <summary>
    /// Nhận giá trị chỉ ra liệu giao thức có nên giữ kết nối mở sau khi nhận được một gói tin hay không.
    /// </summary>
    bool KeepConnectionOpen { get; }

    /// <summary>
    /// Xử lý một kết nối mới.
    /// </summary>
    /// <param name="connection">Kết nối.</param>
    /// <param name="ar">Kết quả của việc kết nối.</param>
    void OnAccept(IConnection connection);

    /// <summary>
    /// Xử lý một tin nhắn đến từ kết nối.
    /// </summary>
    /// <param name="sender">Nguồn gốc của sự kiện.</param>
    /// <param name="args">Tham số của sự kiện kết nối.</param>
    void ProcessMessage(object sender, IConnectionEventArgs args);

    /// <summary>
    /// Chạy sau khi xử lý một tin nhắn từ kết nối.
    /// </summary>
    /// <param name="sender">Nguồn gốc của sự kiện.</param>
    /// <param name="args">Tham số của sự kiện kết nối.</param>
    void PostProcessMessage(object sender, IConnectionEventArgs args);
}