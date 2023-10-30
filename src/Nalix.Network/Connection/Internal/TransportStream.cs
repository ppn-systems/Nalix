// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets;
using Nalix.Framework.Time;
using Nalix.Network.Internal;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Connection.Internal;

/// <summary>
/// Manages the socket connection and handles sending/receiving data with caching and logging.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TransportStream"/> class.
/// </remarks>
/// <param name="socket">The socket.</param>
internal class TransportStream(System.Net.Sockets.Socket socket) : System.IDisposable
{
    #region Fields

    private readonly TransportCache _cache = new();
    private readonly System.Net.Sockets.Socket _socket = socket;

    private System.Boolean _disposed;
    private System.Byte[] _buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Rent(256);

    #endregion Fields

    #region Properties

    /// <summary>
    /// Event triggered when the connection is disconnected.
    /// </summary>
    public System.Action? Disconnected;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public System.Int64 UpTime => this._cache.Uptime;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public System.Int64 LastPingTime => this._cache.LastPingTime;

    #endregion Properties

    #region Constructor

    static TransportStream() =>
        ObjectPoolManager.Instance.SetMaxCapacity<PooledSocketAsyncContext>(1024);

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Begins receiving data asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
    {
        if (this._disposed)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[{0}] BeginReceive called on disposed", nameof(TransportStream));
            return;
        }

        try
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[{0}] Starting asynchronous read operation", nameof(TransportStream));

            System.Net.Sockets.SocketAsyncEventArgs args = new();
            args.SetBuffer(this._buffer, 0, 2);

            args.Completed += (sender, args) =>
            {
                // Convert the result to Task to keep the API intact
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new();
                tcs.SetResult(args.BytesTransferred);

                System.Threading.Tasks.Task<System.Int32> receiveTask = tcs.Task;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await this.OnReceiveCompleted(receiveTask, cancellationToken);
                    }
                    catch (System.Net.Sockets.SocketException ex) when
                        (ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Debug("[{0}] _udpListener reset", nameof(TransportStream));
                        this.Disconnected?.Invoke();
                    }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error("[{0}] BeginReceive error: {1}", nameof(TransportStream), ex.Message);
                    }
                }, cancellationToken);
            };

            if (!this._socket.ReceiveAsync(args))
            {
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new();
                tcs.SetResult(args.BytesTransferred);

                System.Threading.Tasks.Task<System.Int32> receiveTask = tcs.Task;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await this.OnReceiveCompleted(receiveTask, cancellationToken);
                }, cancellationToken);
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("[{0}] BeginReceive error: {1}", nameof(TransportStream), ex.Message);
        }
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

        if (data.Length > System.UInt16.MaxValue - sizeof(System.UInt16))
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + sizeof(System.UInt16));

        if (data.Length <= PacketConstants.StackAllocLimit)
        {
            try
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TransportStream)}] Sending data (stackalloc)");

                System.Span<System.Byte> bufferS = stackalloc System.Byte[totalLength];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bufferS, totalLength);

                data.CopyTo(bufferS[sizeof(System.UInt16)..]);
                System.Int32 sent = _socket.Send(bufferS);
                if (sent != bufferS.Length)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error("[{0}] Partial send: {1}/{2}", nameof(TransportStream), sent, bufferS.Length);
                    return false;
                }

                // Note: _cache only supports ReadOnlyMemory<byte>, so convert
                this._cache.PushOutgoing(data.ToArray());
                return true;
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error("[{0}] Send error (stackalloc): {1}", nameof(TransportStream), ex);
                return false;
            }
        }

        System.Byte[] buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                        .Rent(totalLength);

        try
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[{0}] Sending data (pooled)", nameof(TransportStream));

            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                  .AsSpan(buffer), totalLength);
            data.CopyTo(System.MemoryExtensions
                .AsSpan(buffer, sizeof(System.UInt16)));


            _ = _socket.Send(buffer, 0, totalLength, System.Net.Sockets.SocketFlags.None);

            // Note: _cache only supports ReadOnlyMemory<byte>, so convert
            this._cache.PushOutgoing(System.MemoryExtensions.AsMemory(buffer, 2, totalLength));
            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("[{0}] Send error (pooled): {1}", nameof(TransportStream), ex);
            return false;
        }
        finally
        {
            InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Return(buffer);
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

        if (data.Length > System.UInt16.MaxValue - sizeof(System.UInt16))
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + sizeof(System.UInt16));
        System.Byte[] buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Rent(totalLength);

        try
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                  .AsSpan(buffer), totalLength);

            data.Span.CopyTo(System.MemoryExtensions
                     .AsSpan(buffer, sizeof(System.UInt16)));

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[{0}] Sending data async", nameof(TransportStream));

            _ = await this._socket.SendAsync(System.MemoryExtensions
                                  .AsMemory(buffer, 0, totalLength), System.Net.Sockets.SocketFlags.None, cancellationToken);
            this._cache.PushOutgoing(data);

            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("[{0}] SendAsync error: {1}", nameof(TransportStream), ex.Message);
            return false;
        }
        finally
        {
            InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Return(buffer);
        }
    }

    /// <summary>
    /// Gets a copy of incoming packets.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.ReadOnlyMemory<System.Byte> PopIncoming()
    {
        if (this._cache.Incoming.TryPop(out System.ReadOnlyMemory<System.Byte> data))
        {
            return data;
        }

        return System.ReadOnlyMemory<System.Byte>.Empty; // Avoid null
    }

    /// <summary>
    /// Injects raw packet bytes into the incoming cache manually.
    /// </summary>
    /// <param name="data">The raw byte data to inject.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void InjectIncoming(System.Byte[] data)
    {
        if (data.Length == 0 || this._disposed)
        {
            return;
        }

        this._cache.LastPingTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;
        this._cache.PushIncoming(new System.ReadOnlyMemory<System.Byte>(data));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TransportStream)}] Injected {data.Length} bytes into incoming cache.");
    }


    /// <summary>
    /// Registers a callback to be invoked when a packet is cached.
    /// </summary>
    /// <param name="handler">The callback method to register.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetPacketCached(System.Action handler) => this._cache.PacketCached += handler;

    /// <summary>
    /// Unregisters a previously registered callback from the packet cached event.
    /// </summary>
    /// <param name="handler">The callback method to remove.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RemovePacketCached(System.Action handler) => this._cache.PacketCached -= handler;

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Handles the completion of data reception.
    /// </summary>
    /// <param name="task">The task representing the read operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async System.Threading.Tasks.Task OnReceiveCompleted(
        System.Threading.Tasks.Task<System.Int32> task,
        System.Threading.CancellationToken cancellationToken)
    {
        if (task.IsCanceled || this._disposed)
        {
            return;
        }

        try
        {
            System.Int32 totalBytesRead = await task;
            if (totalBytesRead == 0)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TransportStream)}] Clients closed");

                this.Disconnected?.Invoke();
                return;
            }

            if (totalBytesRead < 2)
            {
                return;
            }

            System.Int32 offset = 0;
            System.UInt16 size = System.BitConverter.ToUInt16(this._buffer, offset);
            offset += sizeof(System.UInt16);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[{0}] Packet size: {1} bytes.", nameof(TransportStream), size);

            if (size > InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().MaxBufferSize)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error("[{0}] Size {1} exceeds max {2} ", nameof(TransportStream), size,
                                        InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().MaxBufferSize);

                return;
            }

            if (size > _buffer.Length)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug("[{0}] Renting larger buffer", nameof(TransportStream));

                InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Return(_buffer);
                _buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Rent(size);
            }

            while (totalBytesRead < size)
            {
                System.Int32 bytesRead;
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new();
                PooledSocketAsyncContext saea = ObjectPoolManager.Instance.Get<PooledSocketAsyncContext>();

                try
                {
                    saea.SetBuffer(this._buffer, totalBytesRead, size - totalBytesRead);
                    saea.UserToken = tcs;

                    if (!this._socket.ReceiveAsync(saea))
                    {
                        if (saea.SocketError == System.Net.Sockets.SocketError.Success)
                        {
                            tcs.SetResult(saea.BytesTransferred);
                        }
                        else
                        {
                            tcs.SetException(new System.Net.Sockets.SocketException((System.Int32)saea.SocketError));
                        }
                    }

                    bytesRead = await tcs.Task;
                }
                finally
                {
                    ObjectPoolManager.Instance.Return(saea);
                }

                if (bytesRead == 0)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[{nameof(TransportStream)}] Clients closed during read");

                    this.Disconnected?.Invoke();
                    return;
                }

                totalBytesRead += bytesRead;
            }

            if (totalBytesRead == size)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug("[{0}] Packet received", nameof(TransportStream));

                this._cache.LastPingTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;

                this._cache.PushIncoming(System.MemoryExtensions
                           .AsMemory(this._buffer, 2, totalBytesRead));
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error("[{0}] Incomplete packet", nameof(TransportStream));
            }
        }
        catch (System.Net.Sockets.SocketException ex) when
              (ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[{0}] _udpListener reset", nameof(TransportStream));
            this.Disconnected?.Invoke();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error(ex);
        }
        finally
        {
            this.BeginReceive(cancellationToken);
        }
    }

    /// <summary>
    /// Disposes the managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">If true, releases managed resources; otherwise, only releases unmanaged resources.</param>
    private void Dispose(System.Boolean disposing)
    {
        if (this._disposed)
        {
            return;
        }

        if (disposing)
        {
            InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Return(this._buffer);

            try
            {
                this._socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug("[{0}] Error shutting down socket: {1}", nameof(TransportStream), ex.Message);
            }

            this._socket.Close();

            this._cache.Dispose();
            this._socket.Dispose();
        }

        this._disposed = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug("TransportStream disposed");
    }

    #endregion Private Methods

    /// <summary>
    /// Disposes the resources used by the <see cref="TransportStream"/> instance.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString()
        => $"TransportStream (Clients = {this._socket.RemoteEndPoint}, " +
           $"Disposed = {this._disposed}, UpTime = {this.UpTime}ms, LastPing = {this.LastPingTime}ms)" +
           $"IncomingCount = {this._cache.Incoming.Count}, OutgoingCount = {this._cache.Outgoing.Count} }}";
}