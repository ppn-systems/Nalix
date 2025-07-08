using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Extensions.IO;
using Nalix.Network.Internal;
using Nalix.Shared.Memory.Pools;
using Nalix.Shared.Time;

namespace Nalix.Network.Connection.Transport;

/// <summary>
/// Manages the socket connection and handles sending/receiving data with caching and logging.
/// </summary>
internal class TransportStream : System.IDisposable
{
    #region Fields

    private readonly ILogger? _logger;
    private readonly IBufferPool _pool;
    private readonly TransportCache _cache;
    private readonly System.Net.Sockets.Socket _socket;

    private System.Byte[] _buffer;
    private System.Boolean _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Event triggered when the connection is disconnected.
    /// </summary>
    public System.Action? Disconnected;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public System.Int64 UpTime => _cache.Uptime;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public System.Int64 LastPingTime => _cache.LastPingTime;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportStream"/> class.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="bufferPool">The buffer pool.</param>
    /// <param name="logger">The logger (optional).</param>
    public TransportStream(System.Net.Sockets.Socket socket, IBufferPool bufferPool, ILogger? logger = null)
    {
        _logger = logger;
        _pool = bufferPool;
        _buffer = _pool.Rent();
        _cache = new TransportCache();
        _socket = socket;

        _logger?.Debug("TransportStream created");
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Begins receiving data asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            _logger?.Debug("[{0}] BeginReceive called on disposed", nameof(TransportStream));
            return;
        }

        try
        {
            _logger?.Debug("[{0}] Starting asynchronous read operation", nameof(TransportStream));

            System.Net.Sockets.SocketAsyncEventArgs args = new();
            args.SetBuffer(_buffer, 0, 2);

            args.Completed += (sender, args) =>
            {
                // Convert the result to Task to keep the API intact
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new();
                tcs.SetResult(args.BytesTransferred);

                System.Threading.Tasks.Task<System.Int32> receiveTask = tcs.Task;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await OnReceiveCompleted(receiveTask, cancellationToken);
                    }
                    catch (System.Net.Sockets.SocketException ex) when
                        (ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset)
                    {
                        _logger?.Debug("[{0}] _udpListener reset", nameof(TransportStream));
                        Disconnected?.Invoke();
                    }
                    catch (System.Exception ex)
                    {
                        _logger?.Error("[{0}] BeginReceive error: {1}",
                                      nameof(TransportStream), ex.Message);
                    }
                }, cancellationToken);
            };

            if (!_socket.ReceiveAsync(args))
            {
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new();
                tcs.SetResult(args.BytesTransferred);

                System.Threading.Tasks.Task<System.Int32> receiveTask = tcs.Task;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await OnReceiveCompleted(receiveTask, cancellationToken);
                }, cancellationToken);
            }
        }
        catch (System.Exception ex)
        {
            _logger?.Error("[{0}] BeginReceive error: {1}", nameof(TransportStream), ex.Message);
        }
    }

    /// <summary>
    /// Sends data synchronously using a Span.
    /// </summary>
    /// <param name="data">The data to send as a Span.</param>
    /// <returns>true if the data was sent successfully; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool Send(System.ReadOnlySpan<System.Byte> data)
    {
        try
        {
            if (data.IsEmpty) return false;

            _logger?.Debug("[{0}] Sending data", nameof(TransportStream));
            _socket.Send(data);

            // Note: _cache only supports ReadOnlyMemory<byte>, so convert
            _cache.PushOutgoing(data.ToArray());
            return true;
        }
        catch (System.Exception ex)
        {
            _logger?.Error("[{0}] Send error: {1}", nameof(TransportStream), ex);
            return false;
        }
    }

    /// <summary>
    /// Sends a data asynchronously.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous send operation. The value of the TResult parameter contains true if the data was sent successfully; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.ReadOnlyMemory<System.Byte> data,
        System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            if (data.IsEmpty) return false;

            _logger?.Debug("[{0}] Sending data async", nameof(TransportStream));

            await _socket.SendAsync(data, System.Net.Sockets.SocketFlags.None, cancellationToken);
            _cache.PushOutgoing(data);
            return true;
        }
        catch (System.Exception ex)
        {
            _logger?.Error("[{0}] SendAsync error: {1}", nameof(TransportStream), ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets a copy of incoming packets.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.ReadOnlyMemory<System.Byte> GetIncomingPackets()
    {
        if (_cache.Incoming.TryGetValue(out System.ReadOnlyMemory<System.Byte> data))
            return data;

        return System.ReadOnlyMemory<System.Byte>.Empty; // Avoid null
    }

    /// <summary>
    /// Registers a callback to be invoked when a packet is cached.
    /// </summary>
    /// <param name="handler">The callback method to register.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetPacketCached(System.Action handler) => _cache.PacketCached += handler;

    /// <summary>
    /// Unregisters a previously registered callback from the packet cached event.
    /// </summary>
    /// <param name="handler">The callback method to remove.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RemovePacketCached(System.Action handler) => _cache.PacketCached -= handler;

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
        if (task.IsCanceled || _disposed) return;

        try
        {
            System.Int32 totalBytesRead = await task;
            if (totalBytesRead == 0)
            {
                _logger?.Debug("[{0}] Clients closed", nameof(TransportStream));
                // Close the connection on the server when the client disconnects
                Disconnected?.Invoke();
                return;
            }

            if (totalBytesRead < 2) return;
            System.Int32 offset = 0;

            System.UInt16 size = _buffer.ToUInt16(ref offset);
            _logger?.Debug("[{0}] Packet size: {1} bytes.", nameof(TransportStream), size);

            if (size > _pool.MaxBufferSize)
            {
                _logger?.Error("[{0}] Size {1} exceeds max {2} ",
                                nameof(TransportStream), size, _pool.MaxBufferSize);

                return;
            }

            if (size > _buffer.Length)
            {
                _logger?.Debug("[{0}] Renting larger buffer", nameof(TransportStream));
                _pool.Return(_buffer);
                _buffer = _pool.Rent(size);
            }

            while (totalBytesRead < size)
            {
                System.Int32 bytesRead;
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new();
                PooledSocketAsyncEventArgs saea = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();

                try
                {
                    saea.SetBuffer(_buffer, totalBytesRead, size - totalBytesRead);
                    saea.Completed += (sender, args) =>
                    {
                        if (args.SocketError == System.Net.Sockets.SocketError.Success)
                        {
                            tcs.SetResult(args.BytesTransferred);
                        }
                        else
                        {
                            tcs.SetException(new System.Net.Sockets.SocketException((System.Int32)args.SocketError));
                        }
                    };

                    if (!_socket.ReceiveAsync(saea))
                    {
                        // Nếu hoàn thành đồng bộ, set kết quả thủ công
                        if (saea.SocketError == System.Net.Sockets.SocketError.Success)
                        {
                            tcs.SetResult(saea.BytesTransferred);
                        }
                        else
                        {
                            tcs.SetException(new System.Net.Sockets.SocketException((int)saea.SocketError));
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
                    _logger?.Debug($"[{0}] Clients closed during read", nameof(TransportStream));
                    this.Disconnected?.Invoke();

                    return;
                }

                totalBytesRead += bytesRead;
            }

            if (totalBytesRead == size)
            {
                _logger?.Debug("[{0}] Packet received", nameof(TransportStream));

                _cache.LastPingTime = (long)Clock.UnixTime().TotalMilliseconds;
                _cache.PushIncoming(System.MemoryExtensions.AsMemory(_buffer, 0, totalBytesRead));
            }
            else
            {
                _logger?.Error("[{0}] Incomplete packet", nameof(TransportStream));
            }
        }
        catch (System.Net.Sockets.SocketException ex) when
              (ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset)
        {
            _logger?.Debug("[{0}] _udpListener reset", nameof(TransportStream));
            Disconnected?.Invoke();
        }
        catch (System.Exception ex)
        {
            _logger?.Error(ex);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _pool.Return(_buffer);

            // Đóng socket thay vì đóng stream
            try
            {
                _socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
            }
            catch (System.Exception ex)
            {
                _logger?.Debug("[{0}] Error shutting down socket: {1}", nameof(TransportStream), ex.Message);
            }

            _socket.Close();

            _cache.Dispose();
        }

        _disposed = true;
        _logger?.Debug("TransportStream disposed");
    }

    #endregion Private Methods

    /// <summary>
    /// Disposes the resources used by the <see cref="TransportStream"/> instance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override string ToString()
        => $"TransportStream (Clients = {_socket.RemoteEndPoint}, " +
           $"Disposed = {_disposed}, UpTime = {UpTime}ms, LastPing = {LastPingTime}ms)" +
           $"IncomingCount = {_cache.Incoming.Count}, OutgoingCount = {_cache.Outgoing.Count} }}";
}