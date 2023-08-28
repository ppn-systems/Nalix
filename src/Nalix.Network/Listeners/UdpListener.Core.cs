using Nalix.Common.Caching;
using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Protocols;
using Nalix.Network.Timing;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    #region Constants

    private const System.Int32 ReceiveTimeout = 500; // Milliseconds

    #endregion Constants

    #region Fields

    internal static readonly SocketOptions Config;

    private readonly ILogger _logger;
    private readonly System.UInt16 _port;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;
    private readonly System.Threading.SemaphoreSlim _lock;

    private System.Net.Sockets.UdpClient? _udpClient;
    private System.Threading.CancellationTokenSource? _cts;
    private System.Threading.CancellationToken _cancellationToken;

    private volatile System.Boolean _isDisposed = false;
    private volatile System.Boolean _isRunning = false;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the UDP listener is currently running and listening for datagrams.
    /// </summary>
    public System.Boolean IsListening => this._isRunning;


    /// <summary>
    /// Gets or sets a value indicating whether time synchronization is enabled for the UDP listener.
    /// Throws <see cref="System.InvalidOperationException"/> if set while the listener is running.
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

    static UdpListenerBase() => Config = ConfigurationStore.Instance.Get<SocketOptions>();


    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class with the specified port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    /// <param name="bufferPool">The buffer pool for managing memory buffers.</param>
    /// <param name="logger">The logger instance for logging events.</param>
    protected UdpListenerBase(
        System.UInt16 port,
        IProtocol protocol,
        IBufferPool bufferPool,
        ILogger logger)
    {
        System.ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        System.ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        System.ArgumentNullException.ThrowIfNull(bufferPool, nameof(bufferPool));

        this._port = port;
        this._logger = logger;
        this._protocol = protocol;
        this._bufferPool = bufferPool;

        this._lock = new System.Threading.SemaphoreSlim(1, 1);

        TimeSynchronizer.Instance.ConfigureLogger(logger);
        TimeSynchronizer.Instance.TimeSynchronized += this.SynchronizeTime;

        Config.Port = this._port;
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class using the configured port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    /// <param name="bufferPool">The buffer pool for managing memory buffers.</param>
    /// <param name="logger">The logger instance for logging events.</param>
    protected UdpListenerBase(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : this(Config.Port, protocol, bufferPool, logger)
    {
    }

    #endregion Constructors

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
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
                this._udpClient?.Close();

                TimeSynchronizer.Instance.TimeSynchronized -= this.SynchronizeTime;
            }
            catch { }

            this._lock.Dispose();
        }

        this._isDisposed = true;
        this._logger.Info("UDP Listener disposed");
    }

    #endregion IDisposable
}