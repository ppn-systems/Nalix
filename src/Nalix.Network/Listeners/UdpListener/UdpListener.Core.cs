// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Timing;

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

    [System.Diagnostics.CodeAnalysis.AllowNull] private System.Net.Sockets.UdpClient _udpClient;
    [System.Diagnostics.CodeAnalysis.AllowNull] private System.Threading.CancellationTokenSource _cts;
    private System.Threading.CancellationToken _cancellationToken;

    private System.Int32 _isDisposed = 0;
    private volatile System.Boolean _isRunning = false;

    // Diagnostics fields
    private System.Int64 _rxPackets;
    private System.Int64 _rxBytes;
    private System.Int64 _dropShort;
    private System.Int64 _dropUnauth;
    private System.Int64 _dropUnknown;

    private System.Int64 _recvErrors;

    // Time sync diagnostics
    private System.Int64 _lastSyncUnixMs;
    private System.Int64 _lastDriftMs;
    private System.Int32 _procSeq = -1;

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

        _port = port;
        _protocol = protocol;

        _lock = new System.Threading.SemaphoreSlim(1, 1);

        InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                       .TimeSynchronized += this.SynchronizeTime;

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(UdpListenerBase)}] created port={_port} protocol={protocol.GetType().Name}");
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
        // Atomic check-and-set: 0 -> 1
        if (System.Threading.Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
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

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] disposed");
    }

    #endregion IDisposable
}