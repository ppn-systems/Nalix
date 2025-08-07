// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Low-level socket transport that manages the underlying <see cref="System.Net.Sockets.Socket"/>,
/// a dedicated send-loop, and a receive-loop. It exposes raw byte buffers to higher layers
/// without imposing any framing logic.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{ToString()}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class SocketTransport : System.IDisposable
{
    #region Fields

    private readonly System.Net.Sockets.Socket _socket;
    private readonly ILogger _logger;
    private readonly BufferPoolManager _bufferPool;
    private readonly System.Threading.CancellationTokenSource _cts = new();

    private readonly System.Threading.Channels.Channel<SendItem> _sendChannel;
    private readonly System.Threading.Tasks.Task _sendLoopTask;
    private readonly System.Threading.Tasks.Task _receiveLoopTask;

    private System.Int32 _disposed;             // 0 = no, 1 = yes
    private System.Int32 _closeSignaled;
    private System.Int32 _cancelSignaled;

    /// <summary>
    /// Callback invoked whenever a receive buffer is filled with data.
    /// The callback owns the buffer and MUST return it to the pool or wrap it in a higher-level
    /// abstraction that will eventually return it.
    /// </summary>
    private System.Func<System.Byte[], System.Int32, System.Int32, System.Threading.Tasks.ValueTask> _onReceive; // (buffer, offset, length)

    /// <summary>
    /// Callback invoked exactly once when the transport closes (error or normal).
    /// </summary>
    private System.Action<System.Exception> _onClosed;

    private readonly System.Int32 _receiveBufferSize;

    private readonly struct SendItem(System.Byte[] buffer, System.Int32 offset, System.Int32 length, System.Boolean isPooled)
    {
        public System.Byte[] Buffer { get; } = buffer;
        public System.Int32 Offset { get; } = offset;
        public System.Int32 Length { get; } = length;
        public System.Boolean IsPooled { get; } = isPooled;
    }

    #endregion Fields

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketTransport"/> class.
    /// </summary>
    /// <param name="socket">The connected socket.</param>
    /// <param name="receiveBufferSize">
    /// Initial receive buffer size. The buffer may grow if larger frames are required.
    /// </param>
    internal SocketTransport(System.Net.Sockets.Socket socket, System.Int32 receiveBufferSize = 4096)
    {
        _socket = socket ?? throw new System.ArgumentNullException(nameof(socket));
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>()
                   ?? throw new System.InvalidOperationException("ILogger is not configured.");
        _bufferPool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

        _receiveBufferSize = receiveBufferSize > 0 ? receiveBufferSize : 4096;

        _sendChannel = System.Threading.Channels.Channel.CreateUnbounded<SendItem>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        // Start loops immediately; they will run until cancellation or socket close.
        _sendLoopTask = System.Threading.Tasks.Task.Run(() => SEND_LOOP_ASYNC(_cts.Token));

        _receiveLoopTask = System.Threading.Tasks.Task.Factory.StartNew(
            () => RECEIVE_LOOP_ASYNC(_cts.Token),
            System.Threading.CancellationToken.None,
            System.Threading.Tasks.TaskCreationOptions.LongRunning | System.Threading.Tasks.TaskCreationOptions.DenyChildAttach,
            System.Threading.Tasks.TaskScheduler.Default);
    }

    #endregion Ctor

    #region Properties

    public System.Net.EndPoint RemoteEndPoint => _socket.RemoteEndPoint;

    #endregion Properties

    #region Public APIs

    /// <summary>
    /// Registers the receive and closed callbacks.
    /// </summary>
    /// <param name="onReceive">
    /// Callback invoked whenever bytes are received. The buffer is owned by the callback.
    /// </param>
    /// <param name="onClosed">
    /// Callback invoked exactly once when the transport is closed (with an optional exception).
    /// </param>
    public void SetCallbacks(
        System.Func<System.Byte[], System.Int32, System.Int32, System.Threading.Tasks.ValueTask> onReceive,
        System.Action<System.Exception> onClosed)
    {
        _onReceive = onReceive ?? throw new System.ArgumentNullException(nameof(onReceive));
        _onClosed = onClosed ?? throw new System.ArgumentNullException(nameof(onClosed));
    }

    /// <summary>
    /// Enqueues a pre-framed buffer for sending. The buffer may come from a pool.
    /// </summary>
    /// <param name="buffer">The buffer containing the payload to send.</param>
    /// <param name="offset">The offset into the buffer.</param>
    /// <param name="length">The number of bytes to send.</param>
    /// <param name="isPooled">If true, the buffer will be returned to the pool after sending.</param>
    /// <returns><c>true</c> if enqueued successfully; otherwise, <c>false</c>.</returns>
    public System.Boolean EnqueueSend(System.Byte[] buffer, System.Int32 offset, System.Int32 length, System.Boolean isPooled)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);

        if (System.Threading.Volatile.Read(ref _disposed) != 0)
        {
            if (isPooled)
            {
                _bufferPool.Return(buffer);
            }

            return false;
        }

        if ((System.UInt32)offset > (System.UInt32)buffer.Length ||
            (System.UInt32)length > (System.UInt32)(buffer.Length - offset))
        {
            throw new System.ArgumentOutOfRangeException(nameof(length));
        }

        var item = new SendItem(buffer, offset, length, isPooled);

        if (!_sendChannel.Writer.TryWrite(item))
        {
            if (isPooled)
            {
                _bufferPool.Return(buffer);
            }

            return false;
        }

        return true;
    }

    public void Dispose()
    {
        DISPOSE(true);
        System.GC.SuppressFinalize(this);
    }

    public override System.String ToString()
        => $"SocketTransport(RemoteEndPoint={_socket.RemoteEndPoint}, Disposed={System.Threading.Volatile.Read(ref _disposed) != 0})";

    #endregion Public APIs

    #region Private - Loops

    private async System.Threading.Tasks.Task SEND_LOOP_ASYNC(System.Threading.CancellationToken token)
    {
        try
        {
            while (await _sendChannel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (_sendChannel.Reader.TryRead(out SendItem item))
                {
                    System.Int32 sent = 0;
                    System.Int32 total = item.Length;

                    try
                    {
                        while (sent < total)
                        {
                            System.Int32 n = await _socket.SendAsync(System.MemoryExtensions
                                                          .AsMemory(item.Buffer, item.Offset + sent, total - sent), System.Net.Sockets.SocketFlags.None, token)
                                                          .ConfigureAwait(false);

                            if (n == 0)
                            {
                                // Peer closed or error
                                CANCEL_ONCE();
                                INVOKE_CLOSE_ONCE(null);
                                return;
                            }

                            sent += n;
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        // Cancellation requested; exit loop
                        return;
                    }
                    catch (System.Exception ex)
                    {
                        _logger.Error("[NW.SocketTransport:Internal] send-loop faulted", ex);
                        CANCEL_ONCE();
                        INVOKE_CLOSE_ONCE(ex);
                        return;
                    }
                    finally
                    {
                        if (item.IsPooled)
                        {
                            _bufferPool.Return(item.Buffer);
                        }
                    }
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            _logger.Trace("[NW.SocketTransport:Internal] send-loop cancelled");
        }
        catch (System.Exception ex)
        {
            _logger.Error("[NW.SocketTransport:Internal] send-loop faulted (outer)", ex);
            INVOKE_CLOSE_ONCE(ex);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>")]
    private async System.Threading.Tasks.Task RECEIVE_LOOP_ASYNC(System.Threading.CancellationToken token)
    {
        // We keep a single buffer for raw receive; higher layers may replace it.
        System.Byte[] buffer = _bufferPool.Rent(_receiveBufferSize);

        try
        {
            while (!token.IsCancellationRequested)
            {
                System.Int32 n = await _socket
                    .ReceiveAsync(System.MemoryExtensions
                    .AsMemory(buffer, 0, buffer.Length), System.Net.Sockets.SocketFlags.None, token)
                    .ConfigureAwait(false);

                if (n == 0)
                {
                    // Peer closed
                    CANCEL_ONCE();
                    INVOKE_CLOSE_ONCE(null);
                    return;
                }

                var handler = _onReceive;
                if (handler is null)
                {
                    // No handler registered; drop data but keep running.
                    _logger.Warn("[NW.SocketTransport:ReceiveLoop] No receive handler registered. Dropping bytes.");
                    continue;
                }

                // Hand off ownership of this buffer to upper layer and rent a new one for the next read.
                System.Byte[] current = buffer;
                buffer = _bufferPool.Rent(_receiveBufferSize);

                // Fire and forget; the upper layer owns the buffer (must return or wrap it).
                _ = handler(current, 0, n);
            }
        }
        catch (System.OperationCanceledException)
        {
            _logger.Trace("[NW.SocketTransport:ReceiveLoop] receive-loop cancelled");
        }
        catch (System.Exception ex)
        {
            _logger.Error("[NW.SocketTransport:ReceiveLoop] receive-loop faulted", ex);
            INVOKE_CLOSE_ONCE(ex);
        }
        finally
        {
            _bufferPool.Return(buffer);
            CANCEL_ONCE();
        }
    }

    #endregion Private - Loops

    #region Private - Helpers

    private void DISPOSE(System.Boolean disposing)
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                CANCEL_ONCE();
                _sendChannel.Writer.TryComplete();
            }
            catch
            {
                // ignore
            }

            try { _cts.Cancel(); } catch { /* ignore */ }

            try
            {
                _sendLoopTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // ignore
            }

            try
            {
                _receiveLoopTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                }
            }
            catch { /* ignore */ }

            try
            {
                _socket.Close();
            }
            catch { /* ignore */ }

            _cts.Dispose();
            _socket.Dispose();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CANCEL_ONCE()
    {
        if (System.Threading.Interlocked.Exchange(ref _cancelSignaled, 1) != 0)
        {
            return;
        }

        try { _cts.Cancel(); } catch { /* ignore */ }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void INVOKE_CLOSE_ONCE(System.Exception ex)
    {
        if (System.Threading.Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        try
        {
            _onClosed?.Invoke(ex);
        }
        catch
        {
            // Never throw from close callback
        }
    }

    #endregion Private - Helpers
}