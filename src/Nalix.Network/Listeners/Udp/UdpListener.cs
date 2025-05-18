using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Network.Protocols;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Listeners.Udp;

internal class UdpListener : IDisposable
{
    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _cts;
    private volatile bool _isRunning;

    public int Port { get; }

    public event EventHandler<UdpPacketEventArgs>? PacketReceived;

    public UdpListener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));

        Port = port;
        _udpClient = new UdpClient(Port);
        _cts = new CancellationTokenSource();

        _logger.Info($"[UdpListener] Initialized on port {Port}");
    }

    public void Start()
    {
        if (_isRunning)
        {
            _logger.Warn("[UdpListener] Already running.");
            return;
        }
        _isRunning = true;
        Task.Run(() => ReceiveLoopAsync(_cts.Token));
        _logger.Info("[UdpListener] Started listening.");
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _cts.Cancel();
        _isRunning = false;
        _logger.Info("[UdpListener] Stopped listening.");
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(token);

                // Rent buffer from pool for processing (simulate buffer reuse)
                byte[] buffer = _bufferPool.Rent(result.Buffer.Length);
                Array.Copy(result.Buffer, buffer, result.Buffer.Length);

                // TODO: Decrypt/process packet here using _protocol
                // var decrypted = _protocol.Decrypt(buffer);

                // Raise event with processed data
                PacketReceived?.Invoke(this, new UdpPacketEventArgs(result.RemoteEndPoint, buffer, result.Buffer.Length));

                // Return buffer to pool after processing
                _bufferPool.Return(buffer);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown, ignore
            }
            catch (Exception ex)
            {
                _logger.Error($"[UdpListener] Error receiving UDP packet: {ex.Message}");
            }
        }
    }

    #region IDisposable

    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Stop();
                _udpClient.Dispose();
                _cts.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}

internal class UdpPacketEventArgs : EventArgs
{
    public IPEndPoint RemoteEndPoint { get; }
    public byte[] Buffer { get; }
    public int Length { get; }

    public UdpPacketEventArgs(IPEndPoint remoteEndPoint, byte[] buffer, int length)
    {
        RemoteEndPoint = remoteEndPoint;
        Buffer = buffer;
        Length = length;
    }
}
