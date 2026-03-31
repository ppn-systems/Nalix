// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Tasks;
using Nalix.Network.Configurations;
using Nalix.Network.Internal.Constants;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Throttling;
using Nalix.Network.Timekeeping;

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
    private Socket? _listener;
    private CancellationTokenSource? _cts;
    private CancellationToken _cancellationToken;
    private CancellationTokenRegistration _cancelReg;

    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly TimingWheel s_timing = InstanceManager.Instance.GetOrCreateInstance<TimingWheel>();
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
    /// <exception cref="InternalErrorException"></exception>
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
                throw new InternalErrorException("Cannot change IsTimeSyncEnabled while listening.");
            }

            InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().IsTimeSyncEnabled = value;

            s_logger?.Info("[NW.{Class}] timesync={Timesync}", nameof(TcpListenerBase), value);
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">Gets or sets the port number for the network connection.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    [DebuggerStepThrough]
    protected TcpListenerBase(ushort port, IProtocol protocol)
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

        InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().TimeSynchronized += this.SynchronizeTime;

        PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();
        options.Validate();

        // Configure object pools for accept contexts and socket async event args based on the provided options.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledAcceptContext>(options.AcceptContextCapacity);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledTcpListenerContext>(options.TcpListenerContextCapacity);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledSocketAsyncEventArgs>(options.SocketArgsCapacity);

        // Preallocate objects in the pools to improve performance and reduce latency during runtime.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledAcceptContext>(options.AcceptContextPreallocate);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledTcpListenerContext>(options.TcpListenerContextPreallocate);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledSocketAsyncEventArgs>(options.SocketArgsPreallocate);

        if (OperatingSystem.IsWindows() && s_config.TuneThreadPool)
        {
            int parallelism = Math.Max(Environment.ProcessorCount * MinWorkerThreads, 16);
            // Thread pool optimization for IOCP
            ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            _ = ThreadPool.SetMinThreads(Math.Max(workerThreads, parallelism), Math.Max(completionPortThreads, parallelism));

            ThreadPool.GetMinThreads(out int afterWorker, out int afterIOCP);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info("[NW.{Class}] set-min-threads worker={Worker} iocp={IOCP}", nameof(TcpListenerBase), afterWorker, afterIOCP);
        }
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

        static void cb(object? state)
        {
            if (state is not TcpListenerBase self)
            {
                return;
            }

            try
            {
                try { self._cts?.Cancel(); } catch { }
                try { self._listener?.Close(); } catch { }
                self._listener = null;

                try
                {
                    _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                                .CancelGroup($"{NetworkTags.Net}/{NetworkTags.Tcp}/{self._port}");
                }
                catch { }

                _ = Interlocked.Exchange(ref self._state, (int)ListenerState.STOPPED);

                s_logger?.Info("[NW.{Class}:{Action}] stopped port={Port}", nameof(TcpListenerBase), nameof(SCHEDULE_STOP), self._port);
            }
            catch (Exception ex)
            {
                s_logger?.Error("[NW.{Class}:{Action}] stop-error port={Port} ex={Error}", nameof(TcpListenerBase), nameof(SCHEDULE_STOP), self._port, ex.Message);
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
        this.Dispose(true);
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
            this.Deactivate();

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
                               .TimeSynchronized -= this.SynchronizeTime;
            }
            catch { }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            _lock.Dispose();
        }

        s_logger?.Debug("[NW.{Class}:{Action}] disposed", nameof(TcpListenerBase), nameof(Dispose));
    }

    #endregion IDispose
}
