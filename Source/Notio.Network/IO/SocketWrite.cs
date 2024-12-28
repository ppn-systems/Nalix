using Notio.Common.IMemory;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Notio.Network.IO;

/// <summary>
/// Lớp SocketWriter dùng để gửi dữ liệu tới socket một cách bất đồng bộ.
/// </summary>
public sealed class SocketWriter : IDisposable
{
    private readonly Socket _socket;
    private readonly IBufferAllocator _bufferAllocator;
    private readonly ConcurrentQueue<SocketAsyncEventArgs> _sendArgsPool;
    private readonly SemaphoreSlim _sendLock;
    private readonly CancellationTokenSource _cts;
    private const int MaxPoolSize = 32;
    private volatile bool _disposed;

    /// <summary>
    /// Event được kích hoạt khi có lỗi xảy ra trong quá trình gửi
    /// </summary>
    public event Action<Exception>? OnError;

    /// <summary>
    /// Khởi tạo một đối tượng SocketWriter mới.
    /// </summary>
    /// <param name="socket">Socket dùng để gửi dữ liệu.</param>
    /// <param name="bufferAllocator">Đối tượng quản lý bộ đệm.</param>
    public SocketWriter(Socket socket, IBufferAllocator bufferAllocator)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _bufferAllocator = bufferAllocator ?? throw new ArgumentNullException(nameof(bufferAllocator));
        _sendArgsPool = new ConcurrentQueue<SocketAsyncEventArgs>();
        _sendLock = new SemaphoreSlim(1, 1);
        _cts = new CancellationTokenSource();

        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            var args = CreateSocketAsyncEventArgs();
            _sendArgsPool.Enqueue(args);
        }
    }

    /// <summary>
    /// Gửi dữ liệu bất đồng bộ với timeout.
    /// </summary>
    /// <param name="data">Dữ liệu cần gửi.</param>
    /// <param name="timeout">Thời gian timeout cho quá trình gửi dữ liệu.</param>
    /// <param name="cancellationToken">Token hủy bỏ (tùy chọn).</param>
    /// <returns>Trả về trạng thái thành công của quá trình gửi.</returns>
    public async ValueTask<bool> SendAsync(ReadOnlyMemory<byte> data,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cts.Token);
        cts.CancelAfter(timeout);

        try
        {
            await _sendLock.WaitAsync(cts.Token).ConfigureAwait(false);

            try
            {
                if (!_sendArgsPool.TryDequeue(out var sendArgs))
                {
                    sendArgs = CreateSocketAsyncEventArgs();
                }

                byte[] buffer = _bufferAllocator.RentBuffer(data.Length);
                try
                {
                    data.CopyTo(buffer);
                    sendArgs.SetBuffer(buffer, 0, data.Length);

                    TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                    sendArgs.Completed += OnSendCompleted;

                    if (!_socket.SendAsync(sendArgs))
                    {
                        OnSendCompleted(this, sendArgs);
                    }

                    await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);

                    return sendArgs.SocketError == SocketError.Success;
                }
                finally
                {
                    _bufferAllocator.ReturnBuffer(buffer);

                    if (_sendArgsPool.Count < MaxPoolSize)
                    {
                        sendArgs.Completed -= OnSendCompleted;
                        _sendArgsPool.Enqueue(sendArgs);
                    }
                    else
                    {
                        sendArgs.Dispose();
                    }
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            OnError?.Invoke(ex);
            return false;
        }
    }

    /// <summary>
    /// Gửi dữ liệu đồng bộ.
    /// </summary>
    /// <param name="data">Dữ liệu cần gửi.</param>
    /// <returns>Trả về trạng thái thành công của quá trình gửi.</returns>
    public bool Send(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            byte[] buffer = _bufferAllocator.RentBuffer(data.Length);
            try
            {
                data.CopyTo(buffer);
                return _socket.Send(buffer, 0, data.Length, SocketFlags.None) == data.Length;
            }
            finally
            {
                _bufferAllocator.ReturnBuffer(buffer);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            return false;
        }
    }

    /// <summary>
    /// Tạo một SocketAsyncEventArgs mới.
    /// </summary>
    /// <returns>Một đối tượng SocketAsyncEventArgs mới.</returns>
    private SocketAsyncEventArgs CreateSocketAsyncEventArgs()
    {
        var args = new SocketAsyncEventArgs();
        args.Completed += OnSendCompleted;
        return args;
    }

    /// <summary>
    /// Xử lý sự kiện khi gửi hoàn thành.
    /// </summary>
    private void OnSendCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            OnError?.Invoke(new SocketException((int)e.SocketError));
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _cts.Cancel();
            _cts.Dispose();
            _sendLock.Dispose();

            while (_sendArgsPool.TryDequeue(out var args))
            {
                args.Dispose();
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }
}