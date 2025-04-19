using Notio.Common.Caching;
using Notio.Common.Logging;
using Notio.Network.Configurations;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Network.Listeners;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
public abstract partial class Listener : IListener, IDisposable
{
    #region Constants

    private const int SocketBacklog = 100;
    private const int MaxSimultaneousAccepts = 32;
    private const int AcceptDelay = 10; // Milliseconds
    private const int MinWorkerThreads = 4;

    #endregion

    #region Fields

    private static readonly TcpConfig Config;

    private readonly int _port;
    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _buffer;
    private readonly Socket _listenerSocket;
    private readonly SemaphoreSlim _listenerLock;


    private Thread? _listenerThread;
    private CancellationTokenSource? _cts;

    private volatile bool _isDisposed;
    private volatile bool _isListening = false;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    public bool IsListening => _isListening && _listenerThread?.IsAlive == true;

    #endregion

    #region Constructors

    static Listener() => Config = ConfigurationStore.Instance.Get<TcpConfig>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The port to listen on.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        ArgumentNullException.ThrowIfNull(bufferPool, nameof(bufferPool));

        _port = port;
        _logger = logger;
        _protocol = protocol;
        _buffer = bufferPool;
        _listenerLock = new SemaphoreSlim(1, 1);

        // Create the optimal socket listener.
        _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            ExclusiveAddressUse = !Config.ReuseAddress,
            LingerState = new LingerOption(true, TcpConfig.False) // No need for LingerState if not close soon
        };

        // Increase the queue size on the socket listener.
        _listenerSocket.SetSocketOption(
            SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, Config.BufferSize);

        _listenerSocket.SetSocketOption(
            SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
            Config.ReuseAddress ? TcpConfig.True : TcpConfig.False);

        // Optimized for Socket.IOControlCode on Windows
        if (Config.IsWindows)
        {
            int parallelismLevel = Environment.ProcessorCount * MinWorkerThreads;
            // Thread pool optimization for IOCP
            ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.SetMinThreads(Math.Max(workerThreads, parallelismLevel), completionPortThreads);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : this(Config.Port, protocol, bufferPool, logger)
    {
    }

    #endregion

    #region IDispose

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _logger.Info("Disposing on {0}", _port);

            _cts?.Cancel();
            _cts?.Dispose();

            Interlocked.Exchange(ref _listenerThread, null)?.Join(1000);

            try
            {
                _listenerSocket.Close();
                _listenerSocket.Dispose();
            }
            catch { }

            _listenerLock.Dispose();
        }

        _isDisposed = true;
        _logger.Debug("Listener disposed");
    }

    #endregion
}
