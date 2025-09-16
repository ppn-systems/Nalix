// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Shared.Memory.Buffers;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Manages the socket connection and handles sending/receiving data with caching and logging.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FramedSocketChannel"/> class.
/// </remarks>
/// <param name="socket">The socket.</param>
internal class FramedSocketChannel(System.Net.Sockets.Socket socket) : System.IDisposable
{
    #region Const 

    private const System.Byte HeaderSize = sizeof(System.UInt16);

    #endregion Const

    #region Fields

    private readonly System.Net.Sockets.Socket _socket = socket;
    private readonly System.String _epText = FormatEndpoint(socket);
    private readonly System.Threading.CancellationTokenSource _cts = new();

    private IConnection? _sender;
    private IConnectEventArgs? _cachedArgs;
    private System.EventHandler<IConnectEventArgs>? _callbackPost;
    private System.EventHandler<IConnectEventArgs>? _callbackClose;

    private System.Int32 _disposed;                 // 0 = no, 1 = yes
    private System.Int32 _receiveStarted;           // 0 = not yet, 1 = started
    private System.Int32 _cancelSignaled;           // 0 = not yet, 1 = started
    private System.Byte[] _buffer = BufferLease.Pool.Rent(256);

    #endregion Fields

    #region Properties

    /// <summary>
    /// Caches incoming packets.
    /// </summary>
    public BufferLeaseCache Cache { get; } = new();

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Registers a process to be invoked when a packet is cached. The state is passed back as the argument.
    /// </summary>
    public void SetCallback(
        System.EventHandler<IConnectEventArgs>? close,
        System.EventHandler<IConnectEventArgs>? post,
        IConnection sender, IConnectEventArgs args)
    {
        _callbackPost = post;
        _callbackClose = close;
        _sender = sender ?? throw new System.ArgumentNullException(nameof(sender));
        _cachedArgs = args ?? throw new System.ArgumentNullException(nameof(args));
    }

    /// <summary>
    /// Begins receiving data asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <summary>
    /// Starts the receive loop. The optional <paramref name="cancellationToken"/> can be used
    /// to stop this connection or for coordinated server shutdown. No linked tokens or callbacks.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>")]
    public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Start exactly once
        if (System.Threading.Interlocked.CompareExchange(ref _receiveStarted, 1, 0) != 0)
        {
            return; // already started
        }

#if DEBUG
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(FramedSocketChannel)}] receive-loop started ep={_socket.RemoteEndPoint}");
#endif

        System.Threading.CancellationTokenSource linked =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _ = this.ReceiveLoopAsync(linked.Token).ContinueWith(static (t, state) =>
        {
            var (l, link) = ((ILogger?, System.Threading.CancellationTokenSource))state!;
            if (t.IsFaulted)
            {
                l?.Error($"[{nameof(FramedSocketChannel)}] receive-loop faulted", t.Exception!);
            }

            link.Dispose();
        }, (InstanceManager.Instance.GetExistingInstance<ILogger>(), linked));
    }

    /// <summary>
    /// Sends data synchronously using a Span.
    /// </summary>
    /// <param name="data">The data to send as a Span.</param>
    /// <returns>true if the data was sent successfully; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Send(System.ReadOnlySpan<System.Byte> data)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > System.UInt16.MaxValue - HeaderSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + HeaderSize);

        if (data.Length <= PacketConstants.StackAllocLimit)
        {
            try
            {
#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(FramedSocketChannel)}] send-stackalloc len={data.Length}");
#endif

                System.Span<System.Byte> bufferS = stackalloc System.Byte[totalLength];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bufferS, totalLength);

                data.CopyTo(bufferS[HeaderSize..]);

                System.Int32 sent = 0;
                while (sent < bufferS.Length)
                {
                    System.Int32 n = _socket.Send(bufferS[sent..]);
                    if (n == 0)
                    {
                        this.CancelReceiveOnce();
                        AsyncCallback.Invoke(_callbackClose, _sender!, _cachedArgs!);
                        return false;
                    }
                    sent += n;
                }

                AsyncCallback.Invoke(_callbackPost, _sender!, _cachedArgs!);
                return true;
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(FramedSocketChannel)}] send-stackalloc-error", ex);
                return false;
            }
        }

        System.Byte[] buffer = BufferLease.Pool.Rent(totalLength);

        try
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(FramedSocketChannel)}] send-pooled len={data.Length} id={_sender?.ID}");
#endif

            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                  .AsSpan(buffer), totalLength);

            data.CopyTo(System.MemoryExtensions.AsSpan(buffer, HeaderSize));

            System.Int32 sent = 0;
            while (sent < totalLength)
            {
                System.Int32 n = _socket.Send(buffer, sent, totalLength - sent, System.Net.Sockets.SocketFlags.None);
                if (n == 0)
                {
                    this.CancelReceiveOnce();
                    AsyncCallback.Invoke(_callbackClose, _sender!, _cachedArgs!);
                    return false;
                }
                sent += n;
            }

            AsyncCallback.Invoke(_callbackPost, _sender!, _cachedArgs!);
            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(FramedSocketChannel)}] send-pooled-error id={_sender?.ID}", ex);
            return false;
        }
        finally
        {
            BufferLease.Pool.Return(buffer);
        }
    }

    /// <summary>
    /// Sends a data asynchronously.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous send operation. The value of the TResult parameter contains true if the data was sent successfully; otherwise, false.</returns>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.ReadOnlyMemory<System.Byte> data,
        System.Threading.CancellationToken cancellationToken)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > System.UInt16.MaxValue - HeaderSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + HeaderSize);
        System.Byte[] buffer = BufferLease.Pool.Rent(totalLength);

        try
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                  .AsSpan(buffer), totalLength);

            data.Span.CopyTo(System.MemoryExtensions
                     .AsSpan(buffer, HeaderSize));

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(FramedSocketChannel)}] send-async len={data.Length} id={_sender?.ID}");
#endif

            System.Int32 sent = 0;
            while (sent < totalLength)
            {
                System.Int32 n = await _socket.SendAsync(System.MemoryExtensions
                                              .AsMemory(buffer, sent, totalLength - sent), System.Net.Sockets.SocketFlags.None, cancellationToken)
                                              .ConfigureAwait(false);

                if (n == 0)
                {
                    // peer closed / connection issue
                    this.CancelReceiveOnce();
                    AsyncCallback.Invoke(_callbackClose, _sender!, _cachedArgs!);
                    return false;
                }

                sent += n;
            }

            AsyncCallback.Invoke(_callbackPost, _sender!, _cachedArgs!);
            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(FramedSocketChannel)}] send-async-error id={_sender?.ID}", ex);
            return false;
        }
        finally
        {
            BufferLease.Pool.Return(buffer);
        }
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Safely formats the remote endpoint string without throwing if the socket is already disposed.
    /// </summary>
    private static System.String FormatEndpoint(System.Net.Sockets.Socket s)
    {
        try { return s.RemoteEndPoint?.ToString() ?? "<unknown>"; }
        catch (System.ObjectDisposedException) { return "<disposed>"; }
        catch { return "<unknown>"; }
    }

    /// <summary>
    /// Returns true when the exception indicates a peer-initiated close or shutdown flow
    /// that should be treated as a normal disconnect (not an error).
    /// </summary>
    private static System.Boolean IsBenignDisconnect(System.Exception ex)
    {
        if (ex is System.OperationCanceledException or
            System.ObjectDisposedException)
        {
            return true;
        }

        if (ex is System.Net.Sockets.SocketException se)
        {
            return se.SocketErrorCode
                is System.Net.Sockets.SocketError.ConnectionReset
                or System.Net.Sockets.SocketError.ConnectionAborted
                or System.Net.Sockets.SocketError.Shutdown
                or System.Net.Sockets.SocketError.OperationAborted;
        }

        if (ex is System.IO.IOException ioex && ioex.InnerException is System.Net.Sockets.SocketException ise)
        {
            return ise.SocketErrorCode is System.Net.Sockets.SocketError.ConnectionReset
                or System.Net.Sockets.SocketError.ConnectionAborted
                or System.Net.Sockets.SocketError.Shutdown
                or System.Net.Sockets.SocketError.OperationAborted;
        }

        if (ex is System.AggregateException agg)
        {
            agg = agg.Flatten();
            foreach (var inner in agg.InnerExceptions)
            {
                if (!IsBenignDisconnect(inner))
                {
                    return false;
                }
            }

            return agg.InnerExceptions.Count > 0;
        }

        return false;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.ValueTask ReceiveExactlyAsync(
        System.Memory<System.Byte> dst,
        System.Threading.CancellationToken token)
    {
        System.Int32 read = 0;
        while (read < dst.Length)
        {
            System.Int32 n = await _socket.ReceiveAsync(dst[read..], System.Net.Sockets.SocketFlags.None, token)
                                          .ConfigureAwait(false);
            if (n == 0)
            {
                throw new System.IO.IOException("Peer closed (FIN)",
                    new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.Shutdown));
            }

            read += n;
        }
    }

    private async System.Threading.Tasks.Task ReceiveLoopAsync(System.Threading.CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1) Read 2-byte header (little-endian length)
                await this.ReceiveExactlyAsync(System.MemoryExtensions
                          .AsMemory(_buffer, 0, HeaderSize), token)
                          .ConfigureAwait(false);

                System.UInt16 size = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(System.MemoryExtensions
                                                                           .AsSpan(_buffer, 0, HeaderSize));

#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Meta($"[{nameof(FramedSocketChannel)}] recv-header size(le)={size}");
#endif

                if (size < HeaderSize)
                {
                    throw new System.Net.Sockets.SocketException(
                        (System.Int32)System.Net.Sockets.SocketError.ProtocolNotSupported);
                }

                // 2) Ensure capacity
                if (size > _buffer.Length)
                {
                    BufferLease.Pool.Return(_buffer);
                    _buffer = BufferLease.Pool.Rent(size);
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                          .AsSpan(_buffer, 0, HeaderSize), size);
                }

                // 3) Read payload
                System.Int32 payload = size - HeaderSize;
                await this.ReceiveExactlyAsync(System.MemoryExtensions
                          .AsMemory(_buffer, HeaderSize, payload), token)
                          .ConfigureAwait(false);

#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(FramedSocketChannel)}] " +
                                              $"recv-frame size={size} payload={payload} ep={_epText}");
#endif

                // 4) Handoff to session cache
                this.Cache.LastPingTime = Clock.UnixMillisecondsNow();
                this.Cache.PushIncoming(BufferLease
                          .TakeOwnership(_buffer, HeaderSize, payload));

                _buffer = BufferLease.Pool.Rent(256); // prepare for next read
            }
        }
        catch (System.Exception ex) when (IsBenignDisconnect(ex))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(FramedSocketChannel)}] " +
                                           $"receive-loop ended (peer closed/shutdown) ep={_epText}");
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(FramedSocketChannel)}] receive-loop cancelled");
        }
        catch (System.Exception ex)
        {
            var e = (ex as System.AggregateException)?.Flatten() ?? ex;
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(FramedSocketChannel)}] receive-loop faulted", e);
        }
        finally
        {
            this.CancelReceiveOnce();
            AsyncCallback.Invoke(_callbackClose, _sender!, _cachedArgs!);
        }
    }

    /// <summary>
    /// Disposes the managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">If true, releases managed resources; otherwise, only releases unmanaged resources.</param>
    private void Dispose(System.Boolean disposing)
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            AsyncCallback.Invoke(_callbackClose, _sender!, _cachedArgs!);

            try
            {
                this._socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
            }
            catch { /* ignore */ }

            try
            {
                this._socket.Close();
            }
            catch { /* ignore */ }

            // now it’s safe to return pooled buffer
            BufferLease.Pool.Return(this._buffer);

            this.Cache.Dispose();
            this._socket.Dispose();

            _cts.Dispose();
        }

#if DEBUG
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(FramedSocketChannel)}] disposed");
#endif
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CancelReceiveOnce()
    {
        if (System.Threading.Interlocked.Exchange(ref _cancelSignaled, 1) != 0)
        {
            return;
        }

        try { _cts.Cancel(); } catch { /* ignore */ }
    }

    #endregion Private Methods

    #region Dispose Pattern

    /// <summary>
    /// Disposes the resources used by the <see cref="FramedSocketChannel"/> instance.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString()
        => $"FramedSocketChannel (Client={_socket.RemoteEndPoint}, " +
           $"Disposed={System.Threading.Volatile.Read(ref _disposed) != 0}, " +
           $"UpTime={Cache.Uptime}ms, LastPing={Cache.LastPingTime}ms, " +
           $"IncomingCount={Cache.Incoming.Count})";

    #endregion Dispose Pattern
}