namespace Notio.Network.IO;

/// <summary>
/// Lớp chứa dữ liệu nhận từ socket trong sự kiện.
/// </summary>
/// <remarks>
/// Khởi tạo một instance của <see cref="SocketReceivedEventArgs"/>.
/// </remarks>
/// <param name="data">Dữ liệu byte nhận được từ socket.</param>
public sealed class SocketReceivedEventArgs(byte[] data) : System.EventArgs
{
    /// <summary>
    /// Dữ liệu byte nhận được từ socket.
    /// </summary>
    public byte[] Data { get; } = data;
}