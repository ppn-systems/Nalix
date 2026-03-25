// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Network.Configurations;
using Nalix.Network.Internal;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Throttling;
using Nalix.Network.Timekeeping;
using Nalix.Shared.Memory.Objects;

namespace Nalix.Network.Listeners.Tcp;

[DebuggerNonUserCode]
[SkipLocalsInit]
public abstract partial class TcpListenerBase : IListener
{
    #region Constants

    private const int MinWorkerThreads = 4;

    #endregion Constants

    #region Fields

    private readonly ushort _port;
    private readonly IProtocol _protocol;
    private readonly ConnectionLimiter _limiter;
    private readonly SemaphoreSlim _lock;
    private readonly List<ISnowflake> _acceptWorkerIds;

    private int _state;
    private int _isDisposed;
    private int _stopInitiated;
    private Socket _listener;
    private CancellationTokenSource _cts;
    private CancellationToken _cancellationToken;
    private CancellationTokenRegistration _cancelReg;

    [AllowNull]
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly NetworkSocketOptions s_config = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    private ListenerState State => (ListenerState)Volatile.Read(ref _state);

    /// <summary>
    /// Enables or disables the update loop for the listener.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public bool IsTimeSyncEnabled
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().IsTimeSyncEnabled;

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((ListenerState)Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                throw new InvalidOperationException("Cannot change IsTimeSyncEnabled while listening.");
            }

            InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().IsTimeSyncEnabled = value;

            s_logger?.Info($"[NW.{nameof(TcpListenerBase)}] timesync={value}");
        }
    }

    #endregion Properties

    #region Enums

    private enum ListenerState
    {
        STOPPED = 0,
        STARTING = 1,
        RUNNING = 2,
        STOPPING = 3
    }

    #endregion Enums

    #region Constructors

    static TcpListenerBase() => s_config.Validate();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TcpListenerBase()
    {
        if (OperatingSystem.IsWindows() && s_config.TuneThreadPool)
        {
            int parallelism = Math.Max(Environment.ProcessorCount * MinWorkerThreads, 16);
            // Thread pool optimization for IOCP
            ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            _ = ThreadPool.SetMinThreads(Math.Max(workerThreads, parallelism), Math.Max(completionPortThreads, parallelism));

            ThreadPool.GetMinThreads(out int afterWorker, out int afterIOCP);

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
    [DebuggerStepThrough]
    protected TcpListenerBase(ushort port, IProtocol protocol) : this()
    {
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));

        _isDisposed = 0;

        _port = port;
        _protocol = protocol;
        _state = (int)ListenerState.STOPPED;
        _limiter = InstanceManager.Instance.GetOrCreateInstance<ConnectionLimiter>();

        s_config.Validate();

        _acceptWorkerIds = new(s_config.MaxParallel);
        _lock = new SemaphoreSlim(1, 1);

        InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().TimeSynchronized += SynchronizeTime;

        PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();
        options.Validate();

        // Configure object pools for accept contexts and socket async event args based on the provided options.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledAcceptContext>(options.AcceptContextCapacity);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledListenerProcessContext>(options.ListenerContextCapacity);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledSocketAsyncEventArgs>(options.SocketArgsCapacity);

        // Preallocate objects in the pools to improve performance and reduce latency during runtime.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledAcceptContext>(options.AcceptContextPreallocate);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledListenerProcessContext>(options.ListenerContextPreallocate);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledSocketAsyncEventArgs>(options.SocketArgsPreallocate);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    [DebuggerStepThrough]
    protected TcpListenerBase(IProtocol protocol)
        : this(s_config.Port, protocol)
    {
    }

    #endregion Constructors

    #region Private Methods

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SCHEDULE_STOP()
    {
        if (Interlocked.Exchange(ref _stopInitiated, 1) != 0)
        {
            return;
        }

        static void cb([AllowNull] object state)
        {
            TcpListenerBase self = (TcpListenerBase)state;

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

                _ = Interlocked.Exchange(ref self._state, (int)ListenerState.STOPPED);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] stopped port={self._port}");
            }
            catch (Exception ex)
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

                _ = Interlocked.Exchange(ref self._stopInitiated, 0);
            }
        }

        _ = ThreadPool.UnsafeQueueUserWorkItem(cb, this);
    }

    #endregion Private Methods

    #region IDispose

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    [DebuggerStepThrough]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    /// <param name="disposing">
    /// true to release both managed and unmanaged resources; false to release only unmanaged resources.
    /// </param>
    [DebuggerStepThrough]
    protected virtual void Dispose(bool disposing)
    {
        // Atomic check-and-set: 0 -> 1
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (disposing)
        {
            Deactivate();

            try
            {
                try { _cancelReg.Dispose(); } catch { }

                _cts?.Cancel();
                _cts?.Dispose();

                _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPING);

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
                               .TimeSynchronized -= SynchronizeTime;
            }
            catch { }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            _lock.Dispose();
        }

        s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] disposed");
    }

    #endregion IDispose
}
