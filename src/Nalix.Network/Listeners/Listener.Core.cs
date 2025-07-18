using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Internal;
using Nalix.Network.Protocols;
using Nalix.Network.Security.Guard;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Listeners;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
public abstract partial class Listener : IListener, System.IDisposable
{
    #region Constants

    private const System.Int32 SocketBacklog = 100;
    private const System.Int32 MaxSimultaneousAccepts = 32;
    private const System.Int32 AcceptDelay = 10; // Milliseconds
    private const System.Int32 MinWorkerThreads = 4;

    #endregion Constants

    #region Fields

    internal static readonly SocketSettings Config;

    private readonly ILogger _logger;
    private readonly System.Int32 _port;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;
    private readonly TimeSynchronizer _timeSyncWorker;
    private readonly System.Net.Sockets.Socket _listener;
    private readonly System.Threading.SemaphoreSlim _lock;
    private readonly ConnectionLimiter _connectionLimiter;

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
        get => this._timeSyncWorker.IsTimeSyncEnabled;
        set
        {
            if (this._isRunning)
            {
                throw new System.InvalidOperationException("Cannot change IsTimeSyncEnabled while listening.");
            }

            this._timeSyncWorker.IsTimeSyncEnabled = value;
        }
    }

    #endregion Properties

    #region Constructors

    static Listener() => Config = ConfigurationStore.Instance.Get<SocketSettings>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">Gets or sets the port number for the network connection.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(System.UInt16 port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    {
        System.ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        System.ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        System.ArgumentNullException.ThrowIfNull(bufferPool, nameof(bufferPool));

        this._port = port;
        this._logger = logger;
        this._protocol = protocol;
        this._bufferPool = bufferPool;
        this._connectionLimiter = new ConnectionLimiter(logger);
        this._lock = new System.Threading.SemaphoreSlim(1, 1);

        // Create the optimal socket listener.
        this._listener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp)
        {
            ExclusiveAddressUse = !Config.ReuseAddress,
            // No need for LingerState if not close soon
            LingerState = new System.Net.Sockets.LingerOption(true, SocketSettings.False)
        };

        // Increase the queue size on the socket listener.
        this._listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReceiveBuffer, Config.BufferSize);

        this._listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress,
            Config.ReuseAddress ? SocketSettings.True : SocketSettings.False);

        System.Net.EndPoint remote = new System.Net.IPEndPoint(System.Net.IPAddress.Any, Config.Port);
        this._logger.Debug("[TCP] TCP socket bound to {0}", remote);

        // Bind and Listen
        this._listener.Bind(remote);
        this._listener.Listen(SocketBacklog);

        // Optimized for _udpListener.IOControlCode on Windows
        if (Config.IsWindows)
        {
            System.Int32 parallelismLevel = System.Environment.ProcessorCount * MinWorkerThreads;
            // Thread pool optimization for IOCP
            System.Threading.ThreadPool.GetMinThreads(out System.Int32 workerThreads, out System.Int32 completionPortThreads);
            _ = System.Threading.ThreadPool.SetMinThreads(System.Math.Max(workerThreads, parallelismLevel), completionPortThreads);

            System.Threading.ThreadPool.GetMinThreads(out var afterWorker, out var afterIOCP);
            this._logger.Info("SetMinThreads: worker={0}, IOCP={1}", afterWorker, afterIOCP);
        }

        this._timeSyncWorker = new TimeSynchronizer(logger);

        this._timeSyncWorker.TimeSynchronized += this.SynchronizeTime;

        _ = ObjectPoolManager.Instance.Prealloc<PacketContext<PooledSocketAsyncContext>>(60);
        _ = ObjectPoolManager.Instance.Prealloc<PooledSocketAsyncEventArgs>(30);
        _ = ObjectPoolManager.Instance.Prealloc<PooledAcceptContext>(30);
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
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
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
                this._listener.Close();
                this._listener.Dispose();
            }
            catch { }

            this._lock.Dispose();
        }

        this._isDisposed = true;
        this._logger.Info("Listener disposed");
    }

    #endregion IDispose
}