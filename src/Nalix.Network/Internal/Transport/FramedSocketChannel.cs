// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Framework.Time;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Buffers;

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
    private readonly System.Threading.CancellationTokenSource _cts = new();

    private IConnection? _sender;
    private IConnectEventArgs? _cachedArgs;
    private System.EventHandler<IConnectEventArgs>? _callback;

    private System.Int32 _disposed;                 // 0 = no, 1 = yes
    private System.Int32 _receiveStarted;           // 0 = not yet, 1 = started
    private System.Int32 _disconnectSignaled;       // 0 = not yet, 1 = signaled
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
    /// Registers a callback to be invoked when a packet is cached. The state is passed back as the argument.
    /// </summary>
    public void SetCallback(
        System.EventHandler<IConnectEventArgs>? callback,
        IConnection sender, IConnectEventArgs args)
    {
        _callback = callback;
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
    public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Volatile.Read(ref _disposed) != 0 ||
            System.Threading.Volatile.Read(ref _disconnectSignaled) != 0)
        {
            return;
        }

        // Start exactly once
        if (System.Threading.Interlocked.CompareExchange(ref _receiveStarted, 1, 0) != 0)
        {
            return; // already started
        }

        System.Threading.CancellationTokenSource linked =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _ = this.ReceiveLoopAsync(linked.Token)
                .ContinueWith(static (t, state) => ((System.Threading.CancellationTokenSource)state!).Dispose(), linked, cancellationToken);
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
                                        .Debug($"[{nameof(FramedSocketChannel)}] Sending data (stackalloc)");
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
                        this.OnDisconnected();
                        return false;
                    }
                    sent += n;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(FramedSocketChannel)}] Send error (stackalloc): {ex}");
                return false;
            }
        }

        System.Byte[] buffer = BufferLease.Pool.Rent(totalLength);

        try
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(FramedSocketChannel)}] Sending data (pooled)");
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
                    this.OnDisconnected();
                    return false;
                }
                sent += n;
            }
            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(FramedSocketChannel)}] Send error (pooled): {ex}");
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
                                    .Debug($"[{nameof(FramedSocketChannel)}] Sending data async");
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
                    this.OnDisconnected();
                    return false;
                }

                sent += n;
            }

            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(FramedSocketChannel)}] SendAsync error: {ex.Message}");
            return false;
        }
        finally
        {
            BufferLease.Pool.Return(buffer);
        }
    }

    #endregion Public Methods

    #region Private Methods

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
                throw new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.ConnectionReset);
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

                // 4) Handoff to session cache
                this.Cache.LastPingTime = Clock.UnixMillisecondsNow();
                this.Cache.PushIncoming(BufferLease
                          .TakeOwnership(_buffer, HeaderSize, payload));

                _buffer = BufferLease.Pool.Rent(256); // prepare for next read
            }
        }
        catch (System.OperationCanceledException) { /* normal shutdown */ }
        catch (System.ObjectDisposedException)
        {
            this.OnDisconnected();
        }
        catch (System.Net.Sockets.SocketException)
        {
            this.OnDisconnected();
        }
        catch (System.Exception)
        {
            this.OnDisconnected();
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
            this.OnDisconnected();

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
    private void OnDisconnected()
    {
        // Ensure "exactly once" across threads
        if (System.Threading.Interlocked.Exchange(ref _disconnectSignaled, 1) != 0)
        {
            return;
        }

        // Notify subscribers; do not let one bad handler kill the rest
        _cts.Cancel();
        _callback?.Invoke(_sender!, _cachedArgs!);
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