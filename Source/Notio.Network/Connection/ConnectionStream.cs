using Notio.Common.Logging.Interfaces;
using Notio.Common.Memory.Pools;
using Notio.Shared.Memory.Cache;
using Notio.Shared.Time;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Connection;

/// <summary>
/// Manages the network stream and handles sending/receiving data with caching and logging.
/// </summary>
public class ConnectionStream : IDisposable
{
    private readonly ILogger? _logger;
    private readonly NetworkStream _stream;
    private readonly IBufferPool _bufferPool;

    private byte[] _buffer;
    private bool _disposed;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public long LastPingTime { get; private set; }

    /// <summary>
    /// Cache for outgoing packets.
    /// </summary>
    public readonly BinaryCache CacheOutgoingPacket;

    /// <summary>
    /// Cache for incoming packets.
    /// </summary>
    public readonly FifoCache<byte[]> CacheIncomingPacket;

    /// <summary>
    /// Event triggered when a new packet is added to the cache.
    /// </summary>
    public Action? PacketCached;

    /// <summary>
    /// A delegate that processes received data.
    /// </summary>
    public Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? TransformReceivedData;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionStream"/> class.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="bufferPool">The buffer pool.</param>
    /// <param name="logger">The logger (optional).</param>
    public ConnectionStream(Socket socket, IBufferPool bufferPool, ILogger? logger = null)
    {
        _logger = logger;
        _bufferPool = bufferPool;
        _buffer = _bufferPool.Rent(256);
        _stream = new NetworkStream(socket);
        CacheOutgoingPacket = new BinaryCache(20);
        CacheIncomingPacket = new FifoCache<byte[]>(20);
    }

    /// <summary>
    /// Begins receiving data asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        try
        {
            _stream.ReadAsync(_buffer, 0, 2, cancellationToken)
                   .ContinueWith((task, state) => OnReceiveCompleted(task, cancellationToken), cancellationToken);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
            _logger?.Debug("Connection reset by remote host.");
        }
        catch (IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
        {
            _logger?.Debug("Connection forcibly closed by remote host.");
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    /// <summary>
    /// Sends a message synchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>true if the message was sent successfully; otherwise, false.</returns>
    public bool Send(ReadOnlySpan<byte> message)
    {
        try
        {
            // Validate that the message is long enough to extract the key.
            if (message.Length < 9)
                throw new ArgumentException("Message must be at least 9 bytes long", nameof(message));

            // Allocate a key of 9 bytes.
            Span<byte> key = stackalloc byte[9];
            message[..4].CopyTo(key[..4]);
            message.Slice(message.Length - 5, 5).CopyTo(key.Slice(4, 5));

            // Cache the message if the key is not already present.
            if (!CacheOutgoingPacket.TryGetValue(key, out _))
            {
                CacheOutgoingPacket.Add(key, message.ToArray());
            }

            _stream.Write(message);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
            return false;
        }
    }

    /// <summary>
    /// Sends a message asynchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous send operation. The value of the TResult parameter contains true if the message was sent successfully; otherwise, false.</returns>
    public async Task<bool> SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (message.Length < 1)
                throw new ArgumentException("Message must be at least 1 bytes long", nameof(message));

            Span<byte> key = stackalloc byte[1];
            message.AsSpan(0, 1).CopyTo(key[0..]);

            if (!CacheOutgoingPacket.TryGetValue(key, out _))
            {
                CacheOutgoingPacket.Add(key, message);
            }

            await _stream.WriteAsync(message, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
            return false;
        }
    }

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
            int totalBytesRead = task.Result;
            if (totalBytesRead < 2) return;

            ushort size = BitConverter.ToUInt16(_buffer, 0);
            if (size > _bufferPool.MaxBufferSize)
            {
                _logger?.Error($"Data length ({size} bytes) exceeds the maximum allowed buffer size ({_bufferPool.MaxBufferSize} bytes).");
                return;
            }

            if (size > _buffer.Length)
                _buffer = _bufferPool.Rent(size);

            while (totalBytesRead < size)
            {
                int bytesRead = await _stream.ReadAsync(_buffer.AsMemory(totalBytesRead, size - totalBytesRead), cancellationToken);
                if (bytesRead == 0) break;
                totalBytesRead += bytesRead;
            }

            ReadOnlyMemory<byte> receivedData = _buffer.AsMemory(0, totalBytesRead);
            byte[] processedData = TransformReceivedData?.Invoke(receivedData).ToArray() ?? receivedData.ToArray();

            CacheIncomingPacket.Add(processedData);
            LastPingTime = (long)Clock.UnixTime().TotalMilliseconds;
            PacketCached?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex);
        }
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="ConnectionStream"/> instance.
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
            _buffer = [];
            _stream.Dispose();
        }

        CacheOutgoingPacket.Clear();
        CacheIncomingPacket.Clear();

        _disposed = true;
    }
}