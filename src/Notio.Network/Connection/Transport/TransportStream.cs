using Notio.Common.Caching;
using Notio.Common.Logging;
using Notio.Shared.Time;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection.Transport;

/// <summary>
/// Manages the network stream and handles sending/receiving data with caching and logging.
/// </summary>
internal class TransportStream : IDisposable
{
    private readonly ILogger? _logger;
    private readonly TransportCache _cache;
    private readonly NetworkStream _stream;
    private readonly IBufferPool _bufferPool;

    private byte[] _buffer;
    private bool _disposed;

    /// <summary>
    /// Event triggered when the connection is disconnected.
    /// </summary>
    public Action? Disconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportStream"/> class.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="bufferPool">The buffer pool.</param>
    /// <param name="logger">The logger (optional).</param>
    public TransportStream(Socket socket, IBufferPool bufferPool, ILogger? logger = null)
    {
        _logger = logger;
        _bufferPool = bufferPool;
        _buffer = _bufferPool.Rent();
        _cache = new TransportCache();
        _stream = new NetworkStream(socket);

        _logger?.Debug("TransportStream created");
    }

    /// <summary>
    /// Begins receiving data asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void BeginReceive(CancellationToken cancellationToken = default)
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
                       catch (IOException ex)
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
    public async Task<bool> SendAsync(
        ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
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
    public ReadOnlyMemory<byte> GetIncomingPackets()
    {
        if (_cache.Incoming.TryGetValue(out ReadOnlyMemory<byte> data))
            return data;

        return ReadOnlyMemory<byte>.Empty; // Avoid null
    }

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public long UpTime => _cache.Uptime;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public long LastPingTime => _cache.LastPingTime;

    /// <summary>
    /// Registers a callback to be invoked when a packet is cached.
    /// </summary>
    /// <param name="handler">The callback method to register.</param>
    public void SetPacketCached(Action handler) => _cache.PacketCached += handler;

    /// <summary>
    /// Unregisters a previously registered callback from the packet cached event.
    /// </summary>
    /// <param name="handler">The callback method to remove.</param>
    public void RemovePacketCached(Action handler) => _cache.PacketCached -= handler;

    /// <summary>
    /// Handles the completion of data reception.
    /// </summary>
    /// <param name="task">The task representing the read operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task OnReceiveCompleted(Task<int> task, CancellationToken cancellationToken)
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

            if (size > _bufferPool.MaxBufferSize)
            {
                _logger?.Error("[{0}] Size {1} exceeds max {2} ",
                                nameof(TransportStream), size, _bufferPool.MaxBufferSize);

                return;
            }

            if (size > _buffer.Length)
            {
                _logger?.Debug("[{0}] Renting larger buffer", nameof(TransportStream));
                _bufferPool.Return(_buffer);
                _buffer = _bufferPool.Rent(size);
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
        catch (IOException ex)
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
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">If true, releases managed resources; otherwise, only releases unmanaged resources.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _bufferPool.Return(_buffer);
            _stream.Dispose();

            _cache.Dispose();
        }

        _disposed = true;
        _logger?.Debug("TransportStream disposed");
    }
}
