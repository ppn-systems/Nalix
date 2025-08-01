using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Internal;
using Nalix.Network.Listeners.Core;
using Nalix.Network.Protocols;
using Nalix.Network.Throttling;
using Nalix.Network.Timing;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase : IListener, System.IDisposable
{
    #region Constants

    private const System.Int32 SocketBacklog = 100;
    private const System.Int32 MaxSimultaneousAccepts = 32;
    private const System.Int32 AcceptDelay = 10; // Milliseconds
    private const System.Int32 MinWorkerThreads = 4;

    #endregion Constants

    #region Fields

    internal static readonly SocketOptions Config;

    private readonly ILogger _logger;
    private readonly System.UInt16 _port;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;
    private readonly System.Threading.Lock _socketLock;

    private readonly System.Threading.SemaphoreSlim _lock;
    private readonly ConnectionLimiter _connectionLimiter;

    private System.Net.Sockets.Socket? _listener;
    private System.Threading.CancellationTokenSource? _cts;
    private System.Threading.CancellationToken _cancellationToken;

    private volatile System.Boolean _isDisposed = false;
    private volatile System.Boolean _isRunning = false;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    public System.Boolean IsListening => this._isRunning;

    /// <summary>
    /// Enables or disables the update loop for the listener.
    /// </summary>
    public System.Boolean IsTimeSyncEnabled
    {
        get => TimeSynchronizer.Instance.IsTimeSyncEnabled;
        set
        {
            if (this._isRunning)
            {
                throw new System.InvalidOperationException("Cannot change IsTimeSyncEnabled while listening.");
            }

            TimeSynchronizer.Instance.IsTimeSyncEnabled = value;
        }
    }

    #endregion Properties

    #region Constructors

    static TcpListenerBase() => Config = ConfigurationStore.Instance.Get<SocketOptions>();

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">Gets or sets the port number for the network connection.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected TcpListenerBase(System.UInt16 port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    {
        System.ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        System.ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        System.ArgumentNullException.ThrowIfNull(bufferPool, nameof(bufferPool));

        this._port = port;
        this._logger = logger;
        this._protocol = protocol;
        this._bufferPool = bufferPool;
        this._connectionLimiter = new ConnectionLimiter(logger);

        this._socketLock = new();
        this._lock = new System.Threading.SemaphoreSlim(1, 1);



        // Optimized for _udpListener.IOControlCode on Windows
        if (Config.IsWindows)
        {
            System.Int32 parallelismLevel = System.Environment.ProcessorCount * MinWorkerThreads;
            // Thread pool optimization for IOCP
            System.Threading.ThreadPool.GetMinThreads(out System.Int32 workerThreads, out System.Int32 completionPortThreads);
            _ = System.Threading.ThreadPool.SetMinThreads(
                 System.Math.Max(workerThreads, parallelismLevel),
                 System.Math.Max(completionPortThreads, parallelismLevel));

            System.Threading.ThreadPool.GetMinThreads(out var afterWorker, out var afterIOCP);
            this._logger.Info("SetMinThreads: worker={0}, IOCP={1}", afterWorker, afterIOCP);
        }

        TimeSynchronizer.Instance.ConfigureLogger(logger);

        TimeSynchronizer.Instance.TimeSynchronized += this.SynchronizeTime;

        _ = ObjectPoolManager.Instance.Prealloc<PooledSocketAsyncEventArgs>(60);
        _ = ObjectPoolManager.Instance.Prealloc<PooledAcceptContext>(30);

        _ = ObjectPoolManager.Instance.SetMaxCapacity<PooledAcceptContext>(1024);
        _ = ObjectPoolManager.Instance.SetMaxCapacity<PooledSocketAsyncEventArgs>(1024);

        Config.Port = this._port;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected TcpListenerBase(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : this(Config.Port, protocol, bufferPool, logger)
    {
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
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    /// <param name="disposing">
    /// true to release both managed and unmanaged resources; false to release only unmanaged resources.
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected virtual void Dispose(System.Boolean disposing)
    {
        if (this._isDisposed)
        {
            return;
        }

        if (disposing)
        {
            this._cts?.Cancel();
            this._cts?.Dispose();

            try
            {
                this._listener?.Close();
                this._listener?.Dispose();

                TimeSynchronizer.Instance.TimeSynchronized -= this.SynchronizeTime;
            }
            catch { }

            this._lock.Dispose();
        }

        this._isDisposed = true;
        this._logger.Info("TcpListenerBase disposed");
    }

    #endregion IDispose
}