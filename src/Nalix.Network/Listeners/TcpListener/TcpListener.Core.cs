// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Internal;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Timing;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Listeners.Tcp;

[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public abstract partial class TcpListenerBase : IListener, IReportable
{
    #region Constants

    private const System.Int32 MinWorkerThreads = 4;

    #endregion Constants

    #region Fields

    internal static readonly NetworkSocketOptions Config;

    private readonly System.UInt16 _port;
    private readonly IProtocol _protocol;
    private readonly System.Threading.SemaphoreSlim _lock;
    private readonly System.Collections.Generic.List<ISnowflake> _acceptWorkerIds;

    private System.Int32 _state;
    private System.Int32 _isDisposed;
    private System.Int32 _stopInitiated;
    private System.Threading.CancellationToken _cancellationToken;
    private System.Threading.CancellationTokenRegistration _cancelReg;
    private System.Net.Sockets.Socket _listener;
    private System.Threading.CancellationTokenSource _cts;

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
            if ((ListenerState)System.Threading.Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                throw new System.InvalidOperationException("Cannot change IsTimeSyncEnabled while listening.");
            }

            InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().IsTimeSyncEnabled = value;

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[NW.{nameof(TcpListenerBase)}] timesync={value}");
        }
    }

    #endregion Properties

    #region Enums

    private enum ListenerState : System.Int32
    {
        STOPPED = 0,
        STARTING = 1,
        RUNNING = 2,
        STOPPING = 3
    }

    #endregion Enums

    #region Constructors

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static TcpListenerBase()
    {
        Config = ConfigurationManager.Instance.Get<NetworkSocketOptions>();

        if (System.OperatingSystem.IsWindows() && Config.TuneThreadPool)
        {
            System.Int32 parallelism = System.Math.Max(System.Environment.ProcessorCount * MinWorkerThreads, 16);
            // Thread pool optimization for IOCP
            System.Threading.ThreadPool.GetMinThreads(out System.Int32 workerThreads, out System.Int32 completionPortThreads);
            _ = System.Threading.ThreadPool.SetMinThreads(System.Math.Max(workerThreads, parallelism), System.Math.Max(completionPortThreads, parallelism));

            System.Threading.ThreadPool.GetMinThreads(out System.Int32 afterWorker, out System.Int32 afterIOCP);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[NW.{nameof(TcpListenerBase)}] set-min-threads worker={afterWorker} iocp={afterIOCP}");
        }
    }

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

        _isDisposed = 0;

        _port = port;
        _protocol = protocol;
        _state = (System.Int32)ListenerState.STOPPED;

        _acceptWorkerIds = new(Config.MaxParallel);
        _lock = new System.Threading.SemaphoreSlim(1, 1);

        InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().TimeSynchronized += this.SynchronizeTime;

        PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledAcceptContext>(options.AcceptContextMaxCapacity);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledSocketAsyncEventArgs>(options.SocketArgsMaxCapacity);

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledAcceptContext>(options.AcceptContextPreallocate);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledSocketAsyncEventArgs>(options.SocketArgsPreallocate);
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

    #region Private Methods

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void SCHEDULE_STOP()
    {
        if (System.Threading.Interlocked.Exchange(ref _stopInitiated, 1) != 0)
        {
            return;
        }

        static void cb([System.Diagnostics.CodeAnalysis.AllowNull] System.Object state)
        {
            TcpListenerBase self = (TcpListenerBase)state!;

            try
            {
                try { self._cts?.Cancel(); } catch { }
                try { self._listener?.Close(); } catch { }
                self._listener = null;

                try
                {
                    _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                                .CancelGroup($"{NetTaskNames.Net}/{NetTaskNames.Tcp}/{self._port}");
                }
                catch { }

                _ = System.Threading.Interlocked.Exchange(ref self._state, (System.Int32)ListenerState.STOPPED);
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] stop-error port={self._port} ex={ex.Message}");
            }
            finally
            {
                try
                {
                    self._cts?.Dispose();
                }
                catch { }
                self._cts = null;

                _ = System.Threading.Interlocked.Exchange(ref self._stopInitiated, 0);
            }
        }

        _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(cb, this);
    }

    #endregion Private Methods

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
        // Atomic check-and-set: 0 -> 1
        if (System.Threading.Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (disposing)
        {
            this.Deactivate();

            try
            {
                try { _cancelReg.Dispose(); } catch { }

                this._cts?.Cancel();
                this._cts?.Dispose();

                _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.STOPPING);

                try
                {
                    _listener?.Close();
                    _listener?.Dispose();
                }
                catch { }
                finally
                {
                    _listener = null;
                }

                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                               .TimeSynchronized -= this.SynchronizeTime;
            }
            catch { }

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.STOPPED);

            this._lock.Dispose();
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] disposed");
    }

    #endregion IDispose
}