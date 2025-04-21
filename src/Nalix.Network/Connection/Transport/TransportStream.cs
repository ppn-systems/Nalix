using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Shared.Time;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Connection.Transport;

/// <summary>
/// Manages the network stream and handles sending/receiving data with caching and logging.
/// </summary>
internal class TransportStream : IDisposable
{
    #region Fields

    private readonly ILogger? _logger;
    private readonly IBufferPool _pool;
    private readonly NetworkStream _stream;
    private readonly TransportCache _cache;

    private byte[] _buffer;
    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>
    /// Event triggered when the connection is disconnected.
    /// </summary>
    public Action? Disconnected;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public long UpTime => _cache.Uptime;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public long LastPingTime => _cache.LastPingTime;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportStream"/> class.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="bufferPool">The buffer pool.</param>
    /// <param name="logger">The logger (optional).</param>
    public TransportStream(Socket socket, IBufferPool bufferPool, ILogger? logger = null)
    {
        _logger = logger;
        _pool = bufferPool;
        _buffer = _pool.Rent();
        _cache = new TransportCache();
        _stream = new NetworkStream(socket);

        _logger?.Debug("TransportStream created");
    }

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
            _stream.ReadAsync(_buffer, 0, 2, cancellationToken)
                   .ContinueWith(async (task, state) =>
                   {
                       TransportStream self = (TransportStream)state!;
                       try
                       {
                           await self.OnReceiveCompleted(task, cancellationToken);
                       }
                       catch (System.IO.IOException ex)
                       when (ex.InnerException is SocketException se &&
                             se.SocketErrorCode == SocketError.ConnectionReset)
                       {
                           self._logger?.Debug("[{0}] Connection closed by remote", nameof(TransportStream));
                           self.Disconnected?.Invoke();
                       }
                       catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                       {
                           self._logger?.Debug("[{0}] Socket reset", nameof(TransportStream));
                           self.Disconnected?.Invoke();
                       }
                       catch (Exception ex)
                       {
                           self._logger?.Error("[{0}] BeginReceive error: {1}",
                                               nameof(TransportStream), ex.Message);
                       }
                   }, this, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.Error("[{0}] BeginReceive error: {1}", nameof(TransportStream), ex.Message);
        }
    }

    /// <summary>
    /// Sends data synchronously using a Span.
    /// </summary>
    /// <param name="data">The data to send as a Span.</param>
    /// <returns>true if the data was sent successfully; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Send(ReadOnlySpan<byte> data)
    {
        try
        {
            if (data.IsEmpty) return false;

            _logger?.Debug("[{0}] Sending data", nameof(TransportStream));
            _stream.Write(data);

            // Note: _cache only supports ReadOnlyMemory<byte>, so convert
            _cache.PushOutgoing(data.ToArray());
            return true;
        }
        catch (Exception ex)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task<bool> SendAsync(
        ReadOnlyMemory<byte> data,
        System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            if (data.IsEmpty) return false;

            _logger?.Debug("[{0}] Sending data async", nameof(TransportStream));
            await _stream.WriteAsync(data, cancellationToken);

            _cache.PushOutgoing(data);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error("[{0}] SendAsync error: {1}", nameof(TransportStream), ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets a copy of incoming packets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetIncomingPackets()
    {
        if (_cache.Incoming.TryGetValue(out ReadOnlyMemory<byte> data))
            return data;

        return ReadOnlyMemory<byte>.Empty; // Avoid null
    }

    /// <summary>
    /// Registers a callback to be invoked when a packet is cached.
    /// </summary>
    /// <param name="handler">The callback method to register.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPacketCached(Action handler) => _cache.PacketCached += handler;

    /// <summary>
    /// Unregisters a previously registered callback from the packet cached event.
    /// </summary>
    /// <param name="handler">The callback method to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemovePacketCached(Action handler) => _cache.PacketCached -= handler;

    /// <summary>
    /// Handles the completion of data reception.
    /// </summary>
    /// <param name="task">The task representing the read operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async System.Threading.Tasks.Task OnReceiveCompleted(
        System.Threading.Tasks.Task<int> task,
        System.Threading.CancellationToken cancellationToken)
    {
        if (task.IsCanceled || _disposed) return;

        try
        {
            int totalBytesRead = await task;
            if (totalBytesRead == 0)
            {
                _logger?.Debug("[{0}] Client closed", nameof(TransportStream));
                // Close the connection on the server when the client disconnects
                Disconnected?.Invoke();
                return;
            }

            if (totalBytesRead < 2) return;

            ushort size = BitConverter.ToUInt16(_buffer, 0);
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
                int bytesRead = await _stream.ReadAsync(
                    _buffer.AsMemory(totalBytesRead, size - totalBytesRead), cancellationToken);

                if (bytesRead == 0)
                {
                    _logger?.Debug($"[{0}] Client closed during read", nameof(TransportStream));
                    Disconnected?.Invoke();

                    return;
                }

                totalBytesRead += bytesRead;
            }

            if (totalBytesRead == size)
            {
                _logger?.Debug("[{0}] Packet received", nameof(TransportStream));

                _cache.LastPingTime = (long)Clock.UnixTime().TotalMilliseconds;
                _cache.PushIncoming(_buffer.AsMemory(0, totalBytesRead));
            }
            else
            {
                _logger?.Error("[{0}] Incomplete packet", nameof(TransportStream));
            }
        }
        catch (System.IO.IOException ex)
        when (ex.InnerException is SocketException se &&
              se.SocketErrorCode == SocketError.ConnectionReset)
        {
            _logger?.Debug("[{0}] Connection closed by remote", nameof(TransportStream));
            Disconnected?.Invoke();
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
            _logger?.Debug("[{0}] Socket reset", nameof(TransportStream));
            Disconnected?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
        finally
        {
            BeginReceive(cancellationToken);
        }
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="TransportStream"/> instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">If true, releases managed resources; otherwise, only releases unmanaged resources.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _pool.Return(_buffer);
            _stream.Dispose();

            _cache.Dispose();
        }

        _disposed = true;
        _logger?.Debug("TransportStream disposed");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
        => $"TransportStream (Remote = {_stream.Socket?.RemoteEndPoint}, " +
           $"Disposed = {_disposed}, UpTime = {UpTime}ms, LastPing = {LastPingTime}ms)" +
           $"IncomingCount = {_cache.Incoming.Count}, OutgoingCount = {_cache.Outgoing.Count} }}";
}
