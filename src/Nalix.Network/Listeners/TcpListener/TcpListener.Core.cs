// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Throttling;
using Nalix.Network.Timing;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection;
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

    private readonly System.UInt16 _port;
    private readonly IProtocol _protocol;
    private readonly System.Threading.Lock _socketLock;

    private readonly System.Threading.SemaphoreSlim _lock;
    private readonly ConnectionLimiter _connectionLimiter;

    private System.Net.Sockets.Socket? _listener;
    private System.Threading.CancellationTokenSource? _cts;
    private System.Threading.CancellationToken _cancellationToken;

    private volatile System.Boolean _isDisposed = false;
    private System.Int32 _state = (System.Int32)ListenerState.Stopped;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    private ListenerState State => (ListenerState)System.Threading.Volatile.Read(ref _state);

    /// <summary>
    /// Enables or disables the update loop for the listener.
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
            if (System.Threading.Interlocked.CompareExchange(ref _state,
               (System.Int32)ListenerState.Starting,
               (System.Int32)ListenerState.Stopped) != (System.Int32)ListenerState.Stopped)
            {
                throw new System.InvalidOperationException("Cannot change IsTimeSyncEnabled while listening.");
            }

            InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().IsTimeSyncEnabled = value;
        }
    }

    #endregion Properties

    #region Class

    private enum ListenerState : System.Int32
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3
    }

    #endregion Class

    #region Constructors

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static TcpListenerBase() => Config = ConfigurationManager.Instance.Get<SocketOptions>();

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">Gets or sets the port number for the network connection.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    [System.Diagnostics.DebuggerStepThrough]
    protected TcpListenerBase(System.UInt16 port, IProtocol protocol)
    {
        System.ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));

        this._port = port;
        this._protocol = protocol;

        this._socketLock = new();
        this._connectionLimiter = new ConnectionLimiter();
        this._lock = new System.Threading.SemaphoreSlim(1, 1);

        // Optimized for _udpListener.IOControlCode on Windows
        if (Config.IsWindows)
        {
            System.Int32 parallelismLevel = System.Math.Max(System.Environment.ProcessorCount * MinWorkerThreads, 16);
            // Thread pool optimization for IOCP
            System.Threading.ThreadPool.GetMinThreads(out System.Int32 workerThreads, out System.Int32 completionPortThreads);
            _ = System.Threading.ThreadPool.SetMinThreads(
                 System.Math.Max(workerThreads, parallelismLevel),
                 System.Math.Max(completionPortThreads, parallelismLevel));

            System.Threading.ThreadPool.GetMinThreads(out System.Int32 afterWorker, out System.Int32 afterIOCP);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] SetMinThreads: worker={afterWorker}, IOCP={afterIOCP}");
        }

        InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().TimeSynchronized += this.SynchronizeTime;

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledAcceptContext>(1024);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledSocketAsyncEventArgs>(1024);

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledSocketAsyncEventArgs>(60);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledAcceptContext>(30);

        Config.Port = this._port;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    [System.Diagnostics.DebuggerStepThrough]
    protected TcpListenerBase(IProtocol protocol)
        : this(Config.Port, protocol)
    {
    }

    #endregion Constructors

    #region IDispose

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
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
                this._listener?.Close();
                this._listener?.Dispose();

                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                               .TimeSynchronized -= this.SynchronizeTime;
            }
            catch { }

            this._lock.Dispose();
        }

        this._isDisposed = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}] disposed");
    }

    #endregion IDispose
}