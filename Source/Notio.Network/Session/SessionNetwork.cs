using Notio.Common.IMemory;
using Notio.Network.IO;
using System;
using System.Net.Sockets;
using System.Text;

namespace Notio.Network.Session;

/// <summary>
/// Quản lý vận chuyển phiên làm việc, bao gồm gửi và nhận dữ liệu qua socket.
/// </summary>
public sealed class SessionNetwork : IDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// Bộ ghi socket để gửi dữ liệu.
    /// </summary>
    public readonly SocketWriter SocketWriter;

    /// <summary>
    /// Bộ đọc socket để nhận dữ liệu.
    /// </summary>
    public readonly SocketReader SocketReader;

    /// <summary>
    /// Sự kiện kích hoạt khi dữ liệu được nhận.
    /// </summary>
    public event Action<byte[]>? DataReceived;

    /// <summary>
    /// Sự kiện lỗi.
    /// </summary>
    public event Action<string, Exception>? ErrorOccurred;

    /// <summary>
    /// Kiểm tra xem đối tượng đã được giải phóng hay chưa.
    /// </summary>
    public bool IsDispose => _disposed;

    /// <summary>
    /// Khởi tạo một thể hiện mới của lớp <see cref="SessionNetwork"/>.
    /// </summary>
    /// <param name="socket">Socket của khách hàng.</param>
    /// <param name="multiSizeBuffer"></param>
    public SessionNetwork(Socket socket, IBufferAllocator multiSizeBuffer)
    {
        SocketWriter = new(socket, multiSizeBuffer);
        SocketReader = new(socket, multiSizeBuffer);
        SocketReader.DataReceived += OnDataReceived!;
        SocketReader.ErrorOccurred += ErrorOccurred;
    }

    /// <summary>
    /// Gửi dữ liệu dưới dạng mảng byte.
    /// </summary>
    /// <param name="data">Dữ liệu cần gửi.</param>
    /// <returns>True nếu gửi thành công, ngược lại False.</returns>
    public bool Send(byte[] data)
    {
        try
        {
            SocketWriter.Send(data);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Error sending byte array: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Gửi dữ liệu dưới dạng chuỗi.
    /// </summary>
    /// <param name="data">Dữ liệu cần gửi dưới dạng chuỗi.</param>
    /// <returns>True nếu gửi thành công, ngược lại False.</returns>
    public bool Send(string data)
    {
        try
        {
            SocketWriter.Send(Encoding.UTF8.GetBytes(data));
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Error sending string: {ex.Message}", ex);
            return false;
        }
    }

    private void OnDataReceived(object sender, byte[] e)
        => DataReceived?.Invoke(e);

    /// <summary>
    /// Giải phóng tài nguyên khi không còn sử dụng.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);  // Giúp ngăn không cho finalizer tự động chạy.
    }

    /// <summary>
    /// Giải phóng tài nguyên khi không còn sử dụng.
    /// </summary>
    /// <param name="disposing">True nếu được gọi từ Dispose(), false nếu được gọi từ finalizer.</param>
    public void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Giải phóng các đối tượng quản lý thủ công.
            SocketReader.DataReceived -= OnDataReceived!;

            SocketWriter?.Dispose();
            SocketReader?.Dispose();

            _disposed = true;
        }

        // Giải phóng các tài nguyên không phải managed (nếu có).
    }

    /// <summary>
    /// Finalizer (Destructor) trong trường hợp nếu Dispose chưa được gọi.
    /// </summary>
    ~SessionNetwork()
    {
        Dispose(false);
    }
}