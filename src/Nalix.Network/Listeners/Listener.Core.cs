using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Protocols;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Listeners;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
public abstract partial class Listener : IListener, System.IDisposable
{
    #region Constants

    private const int SocketBacklog = 100;
    private const int MaxSimultaneousAccepts = 32;
    private const int AcceptDelay = 10; // Milliseconds
    private const int MinWorkerThreads = 4;

    #endregion Constants

    #region Fields

    private static readonly SocketConfig Config;

    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _buffer;
    private readonly System.Net.Sockets.Socket _udpListener;
    private readonly System.Net.Sockets.Socket _tcpListener;
    private readonly System.Threading.SemaphoreSlim _listenerLock;

    private System.Threading.CancellationTokenSource? _cts;

    private volatile bool _isDisposed;
    private volatile bool _isListening = false;
    private volatile bool _enableUpdate = false;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    public bool IsListening => _isListening;

    /// <summary>
    /// Enables or disables the update loop for the listener.
    /// </summary>
    public bool EnableUpdate
    {
        get => _enableUpdate;
        set
        {
            if (_isListening)
                throw new System.InvalidOperationException("Cannot change EnableUpdate while listening.");
            _enableUpdate = value;
        }
    }

    #endregion Properties

    #region Constructors

    static Listener() => Config = ConfigurationStore.Instance.Get<SocketConfig>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    {
        System.ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        System.ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        System.ArgumentNullException.ThrowIfNull(bufferPool, nameof(bufferPool));

        _logger = logger;
        _protocol = protocol;
        _buffer = bufferPool;
        _listenerLock = new System.Threading.SemaphoreSlim(1, 1);

        // Create the optimal socket listener.
        _tcpListener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp)
        {
            ExclusiveAddressUse = !Config.ReuseAddress,
            // No need for LingerState if not close soon
            LingerState = new System.Net.Sockets.LingerOption(true, SocketConfig.False)
        };

        // Increase the queue size on the socket listener.
        _tcpListener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReceiveBuffer, Config.BufferSize);

        _tcpListener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress,
            Config.ReuseAddress ? SocketConfig.True : SocketConfig.False);

        _udpListener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Dgram,
            System.Net.Sockets.ProtocolType.Udp)
        {
            ExclusiveAddressUse = !Config.ReuseAddress
        };

        _udpListener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress,
            Config.ReuseAddress ? SocketConfig.True : SocketConfig.False);

        // Optimized for _udpListener.IOControlCode on Windows
        if (Config.IsWindows)
        {
            int parallelismLevel = System.Environment.ProcessorCount * MinWorkerThreads;
            // Thread pool optimization for IOCP
            System.Threading.ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            System.Threading.ThreadPool.SetMinThreads(System.Math.Max(workerThreads, parallelismLevel), completionPortThreads);

            System.Threading.ThreadPool.GetMinThreads(out var afterWorker, out var afterIOCP);
            _logger.Info("SetMinThreads: worker={0}, IOCP={1}", afterWorker, afterIOCP);
        }
    }

    #endregion Constructors

    #region IDispose

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _logger.Info("Disposing on {0}", _port);

            _cts?.Cancel();
            _cts?.Dispose();

            try
            {
                _tcpListener.Close();
                _tcpListener.Dispose();
            }
            catch { }

            _listenerLock.Dispose();
        }

        _isDisposed = true;
        _logger.Debug("Listener disposed");
    }

    #endregion IDispose
}
