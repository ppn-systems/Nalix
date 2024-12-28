using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.IO;

/// <summary>
/// Lớp SocketReader dùng để đọc dữ liệu từ socket một cách không đồng bộ.
/// </summary>
public class SocketReader : IDisposable
{
    private readonly Socket _socket;
    private readonly IMultiSizeBufferPool _multiSizeBuffer;
    private readonly SocketAsyncEventArgs _receiveEventArgs;

    private byte[] _buffer;
    private bool _disposed = false;
    private bool _isReceiving = false;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Sự kiện khi dữ liệu được nhận
    /// </summary>
    public event EventHandler<SocketReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Sự kiện lỗi.
    /// </summary>
    public event Action<string, Exception>? OnError;

    /// <summary>
    /// Kiểm tra xem đối tượng đã được giải phóng hay chưa.
    /// </summary>
    public bool Disposed => _disposed;

    /// <summary>
    /// Kiểm tra xem đối tượng có đang nhận dư liệu.
    /// </summary>
    public bool IsReceiving => _isReceiving;

    /// <summary>
    /// Khởi tạo một đối tượng <see cref="SocketReader"/> mới.
    /// </summary>
    /// <param name="socket">Socket dùng để nhận dữ liệu.</param>
    /// <param name="multiSizeBuffer"></param>
    /// <exception cref="ArgumentNullException">Ném ra khi socket là null.</exception>
    public SocketReader(Socket socket, IMultiSizeBufferPool multiSizeBuffer)
    {
        _cts = new CancellationTokenSource();
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _multiSizeBuffer = multiSizeBuffer ?? throw new ArgumentNullException(nameof(multiSizeBuffer));

        _buffer = _multiSizeBuffer.RentBuffer(256);
        _receiveEventArgs = new SocketAsyncEventArgs();
        _receiveEventArgs.SetBuffer(_buffer, 0, _buffer.Length);
        _receiveEventArgs.Completed += OnReceiveCompleted!;
    }

    /// <summary>
    /// Bắt đầu nhận dữ liệu từ socket.
    /// </summary>
    /// <param name="externalCancellationToken">Token hủy bỏ từ bên ngoài (tùy chọn).</param>
    /// <exception cref="ObjectDisposedException">Khi socket đã bị dispose.</exception>
    public void Receive(CancellationToken? externalCancellationToken = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cts == null || _cts.IsCancellationRequested)
        {
            HandleException(new OperationCanceledException(), "Receive cancelled or already stopped.");
            return;
        }

        if (externalCancellationToken != null)
        {
            // Liên kết token bên ngoài với token nội bộ
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken.Value);
        }

        _isReceiving = true;
        StartReceiving();
    }

    /// <summary>
    /// Bắt đầu quá trình nhận dữ liệu từ socket.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Khi socket đã bị dispose.</exception>
    private void StartReceiving()
    {
        if (_disposed) return;

        try
        {
            if (!_socket.ReceiveAsync(_receiveEventArgs))
            {
                OnReceiveCompleted(this, _receiveEventArgs);
            }
        }
        catch (ObjectDisposedException ex)
        {
            _isReceiving = false;

            Dispose();
            HandleException(ex, $"Socket disposed: {ex.Message}");
        }
        catch (InvalidOperationException)
        {
            _isReceiving = false;

            Dispose();
        }
        catch (Exception ex)
        {
            _isReceiving = false;

            Dispose();
            HandleException(ex, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Xử lý sự kiện hoàn thành nhận dữ liệu.
    /// </summary>
    /// <param name="sender">Đối tượng gửi sự kiện.</param>
    /// <param name="e">Thông tin sự kiện.</param>
    /// <exception cref="InvalidOperationException">Khi có lỗi trong quá trình nhận dữ liệu.</exception>
    private async void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
    {
        try
        {
            if (_disposed) return;

            if (!HandleSocketError(e))
            {
                Dispose();
                throw new InvalidOperationException($"Socket error: {e.SocketError}");
            }

            int bytesRead = e.BytesTransferred;
            if (bytesRead > 0 && e.Buffer != null && bytesRead >= 4)
            {
                ReadOnlySpan<byte> sizeBytes = e.Buffer.AsSpan(0, 4);
                int dataSize = BitConverter.ToInt32(sizeBytes);

                // Kiểm tra kích thước và điều chỉnh bộ đệm nếu cần
                ResizeBufferIfNeeded(dataSize);

                // Tạo sự kiện khi dữ liệu đã đầy đủ
                OnDataReceived(new SocketReceivedEventArgs(e.Buffer.Take(bytesRead).ToArray()));
            }
            else
            {
                await Task.Delay(10).ConfigureAwait(false);
                await Task.Yield();
            }

            // Tiếp tục nhận dữ liệu
            StartReceiving();
        }
        catch (OperationCanceledException)
        {
            _isReceiving = false;

            Dispose();
        }
        catch (Exception ex)
        {
            _isReceiving = false;

            Dispose();
            HandleException(ex, $"Error in OnReceiveCompleted: {ex.Message}");
        }
    }

    /// <summary>
    /// Phương thức gọi sự kiện khi dữ liệu đã nhận đầy đủ.
    /// </summary>
    /// <param name="e">Thông tin sự kiện.</param>
    protected virtual void OnDataReceived(SocketReceivedEventArgs e)
    {
        DataReceived?.Invoke(this, e);
    }

    /// <summary>
    /// Dừng việc nhận dữ liệu và hủy bỏ tất cả hoạt động liên quan.
    /// </summary>
    public void Cancel()
    {
        if (_disposed) return;

        try
        {
            _cts?.Cancel(); // Hủy token
            _cts?.Dispose();
            _cts = null;
            _isReceiving = false;
        }
        catch (Exception ex)
        {
            HandleException(ex, $"Error while cancelling: {ex.Message}");
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên không đồng bộ.
    /// </summary>
    /// <returns>Nhiệm vụ đại diện cho quá trình giải phóng tài nguyên.</returns>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        try
        {
            if (disposing)
            {
                // Hủy đăng ký sự kiện an toàn
                if (_receiveEventArgs != null)
                {
                    _receiveEventArgs.Completed -= OnReceiveCompleted!;
                }

                // Giải phóng bộ đệm
                if (_buffer != null)
                {
                    _multiSizeBuffer.ReturnBuffer(_buffer);
                    _buffer = [];
                }

                // Hủy bỏ cancellation token source
                _cts?.Cancel();
                _cts?.Dispose();

                // Giải phóng các tài nguyên socket
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // Xử lý lỗi nếu socket đã bị giải phóng
                }

                // Đóng socket an toàn
                _socket?.Close(timeout: 1000); // Thêm timeout
            }
        }
        finally
        {
            _disposed = true;
            _isReceiving = false;
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Kiểm tra và điều chỉnh bộ đệm nếu kích thước dữ liệu nhận được lớn hơn kích thước bộ đệm hiện tại.
    /// </summary>
    /// <param name="dataSize">Kích thước dữ liệu nhận được.</param>
    private void ResizeBufferIfNeeded(int dataSize)
    {
        if (dataSize > _buffer.Length)
        {
            _multiSizeBuffer.ReturnBuffer(_buffer);
            _buffer = _multiSizeBuffer.RentBuffer(dataSize);
            _receiveEventArgs.SetBuffer(_buffer, 0, _buffer.Length);
        }
    }

    private void HandleException(Exception ex, string message)
    {
        Dispose();
        OnError?.Invoke(message, ex);
    }

    private readonly Func<SocketAsyncEventArgs, bool> HandleSocketError = (e) =>
    {
        return e.SocketError == SocketError.Success;
    };
}