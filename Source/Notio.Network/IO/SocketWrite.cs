using Notio.Common.IMemory;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.IO;

/// <summary>
/// Lớp SocketWriter dùng để gửi dữ liệu tới socket một cách bất đồng bộ.
/// </summary>
public sealed class SocketWriter : IDisposable
{
    private const int MaxPoolSize = 32;
    private volatile bool _disposed;
    private readonly Socket _socket;
    private readonly SemaphoreSlim _sendLock;
    private readonly CancellationTokenSource _cts;
    private readonly IBufferAllocator _bufferAllocator;
    private readonly ConcurrentQueue<SocketAsyncEventArgs> _sendArgsPool;

    /// <summary>
    /// Event được kích hoạt khi có lỗi xảy ra trong quá trình gửi
    /// </summary>
    public event Action<string, Exception>? ErrorOccurred;

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

    public async ValueTask<bool> SendAsync(ReadOnlyMemory<byte> data, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        cts.CancelAfter(timeout);

        try
        {
            await _sendLock.WaitAsync(cts.Token).ConfigureAwait(false);

            if (!_sendArgsPool.TryDequeue(out var sendArgs))
            {
                sendArgs = CreateSocketAsyncEventArgs();
            }

            byte[] buffer = _bufferAllocator.RentBuffer(data.Length);
            try
            {
                data.CopyTo(buffer);
                sendArgs.SetBuffer(buffer, 0, data.Length);

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                sendArgs.Completed += (_, e) => tcs.TrySetResult(e.SocketError == SocketError.Success);

                if (!_socket.SendAsync(sendArgs))
                {
                    tcs.TrySetResult(sendArgs.SocketError == SocketError.Success);
                }

                await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                return sendArgs.SocketError == SocketError.Success;
            }
            finally
            {
                _bufferAllocator.ReturnBuffer(buffer);
                RecycleSendArgs(sendArgs);
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            ErrorOccurred?.Invoke("ex Error occurred while sending asynchronously.", ex);
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

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
            ErrorOccurred?.Invoke("ex Error occurred while sending synchronously.", ex);
            return false;
        }
    }

    private static SocketAsyncEventArgs CreateSocketAsyncEventArgs() => new();

    private void RecycleSendArgs(SocketAsyncEventArgs sendArgs)
    {
        if (_sendArgsPool.Count < MaxPoolSize)
        {
            sendArgs.SetBuffer(null, 0, 0);
            _sendArgsPool.Enqueue(sendArgs);
        }
        else
        {
            sendArgs.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
        _sendLock.Dispose();

        while (_sendArgsPool.TryDequeue(out var args))
        {
            args.Dispose();
        }
    }
}