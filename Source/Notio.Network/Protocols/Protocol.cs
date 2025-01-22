using Notio.Common.Connection;
using Notio.Common.Connection.Args;

namespace Notio.Network.Protocols;

/// <summary>
/// Lớp cơ sở đại diện cho một giao thức mạng.
/// </summary>
public abstract class Protocol : IProtocol
{
    /// <summary>
    /// Chỉ ra liệu giao thức có nên giữ kết nối mở sau khi nhận được một gói tin hay không.
    /// </summary>
    public virtual bool KeepConnectionOpen { get; protected set; }

    /// <summary>
    /// Xử lý một kết nối mới.
    /// </summary>
    /// <param name="connection">Kết nối mới.</param>
    public virtual void OnAccept(IConnection connection)
    {
        connection.BeginReceive();

        //Để thực hiện xác thực lệnh cấm IP
    }

    /// <summary>
    /// Chạy sau khi xử lý một tin nhắn từ kết nối.
    /// </summary>
    /// <param name="sender">Nguồn gốc của sự kiện.</param>
    /// <param name="args">Tham số của sự kiện kết nối.</param>
    public void PostProcessMessage(object sender, IConnectEventArgs args)
    {
        if (!KeepConnectionOpen) args.Connection.Disconnect();
    }

    /// <summary>
    /// Xử lý một tin nhắn đến từ kết nối.
    /// </summary>
    /// <param name="sender">Nguồn gốc của sự kiện.</param>
    /// <param name="connection">Tham số của sự kiện kết nối.</param>
    public abstract void ProcessMessage(object sender, IConnectEventArgs connection);
}