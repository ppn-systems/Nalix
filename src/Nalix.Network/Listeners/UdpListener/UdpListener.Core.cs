// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Timing;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    #region Constants

    private const System.Int32 ReceiveTimeout = 500; // Milliseconds

    #endregion Constants

    #region Fields

    internal static readonly NetworkSocketOptions Config;

    private readonly System.UInt16 _port;
    private readonly IProtocol _protocol;
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
        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().IsTimeSyncEnabled;

        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set
        {
            if (this._isRunning)
            {
                throw new System.InvalidOperationException($"[{nameof(UdpListenerBase)}] Cannot change IsTimeSyncEnabled while listening.");
            }

            InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                           .IsTimeSyncEnabled = value;

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(UdpListenerBase)}] timesync={value}");
        }
    }

    #endregion Properties

    #region Constructors

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static UdpListenerBase() => Config = ConfigurationManager.Instance.Get<NetworkSocketOptions>();


    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class with the specified port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    [System.Diagnostics.DebuggerStepThrough]
    protected UdpListenerBase(System.UInt16 port, IProtocol protocol)
    {
        System.ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));

        this._port = port;
        this._protocol = protocol;

        this._lock = new System.Threading.SemaphoreSlim(1, 1);

        InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                       .TimeSynchronized += this.SynchronizeTime;

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(UdpListenerBase)}] created port={_port} protocol={protocol.GetType().Name}");
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class using the configured port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    [System.Diagnostics.DebuggerStepThrough]
    protected UdpListenerBase(IProtocol protocol)
        : this(Config.Port, protocol)
    {
    }

    #endregion Constructors

    #region IDisposable

    /// <inheritdoc/>
    [System.Diagnostics.DebuggerStepThrough]
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    [System.Diagnostics.DebuggerStepThrough]
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

                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                               .TimeSynchronized -= this.SynchronizeTime;
            }
            catch { }

            this._lock.Dispose();
        }

        this._isDisposed = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?.Info($"[{nameof(UdpListenerBase)}] disposed");
    }

    #endregion IDisposable
}