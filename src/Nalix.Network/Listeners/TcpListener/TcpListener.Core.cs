// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Internal.Time;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;

namespace Nalix.Network.Listeners.Tcp;

[SkipLocalsInit]
[DebuggerNonUserCode]
public abstract partial class TcpListenerBase : IListener
{
    #region Fields

    private readonly ushort _port;
    private readonly SemaphoreSlim _lock;
    private readonly IProtocol _protocol;
    private readonly IConnectionHub _hub;
    private readonly ConnectionGuard _limiter;
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

    #endregion Properties

    #region Enums

    // STOPPED -> STARTING -> RUNNING -> STOPPING -> STOPPED
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
    /// <param name="hub">The connection hub for managing active connections.</param>
    [DebuggerStepThrough]
    protected TcpListenerBase(ushort port, IProtocol protocol, IConnectionHub hub)
    {
        ArgumentNullException.ThrowIfNull(hub, nameof(hub));
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));

        _isDisposed = 0;

        _hub = hub;
        _port = port;
        _protocol = protocol;
        _state = (int)ListenerState.STOPPED;
        _limiter = InstanceManager.Instance.GetOrCreateInstance<ConnectionGuard>();

        // Register force-close action to ConnectionGuard for DDoS protection
        _ = _limiter.WithForceClose(key => _hub.ForceClose(key));

        s_config.Validate();

        _lock = new SemaphoreSlim(1, 1);
        _acceptWorkerIds = new(s_config.MaxParallel);

        PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();
        options.Validate();

        // Configure object pools for accept contexts and socket async event args based on the provided options.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledAcceptContext>(options.AcceptContextCapacity);
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledSocketAsyncEventArgs>(options.SocketArgsCapacity);

        // Preallocate objects in the pools to improve performance and reduce latency during runtime.
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
    /// <param name="hub">The connection hub for managing active connections.</param>
    [DebuggerStepThrough]
    protected TcpListenerBase(IProtocol protocol, IConnectionHub hub) : this(s_config.Port, protocol, hub)
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

        static async void cb(object? state)
        {
            if (state is not TcpListenerBase self)
            {
                return;
            }

            bool lockTaken = false;
            try
            {
                // Acquire lock asynchronously to avoid blocking callback threads.
                await self._lock.WaitAsync().ConfigureAwait(false);
                lockTaken = true;

                if (Volatile.Read(ref self._isDisposed) != 0)
                {
                    return;
                }

                // Cancel first -> signal all async loops to stop.
                try
                {
                    self._cts?.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                        $"cts-cancel-ignored port={self._port} reason={ex.GetType().Name}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                        $"cts-cancel-failed port={self._port}", ex);
                }

                // Close socket server -> AcceptAsync will throw SocketException -> loop exits.
                try
                {
                    self._listener?.Close();
                }
                catch (ObjectDisposedException ex)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                        $"listener-close-ignored port={self._port} reason={ex.GetType().Name}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                        $"listener-close-failed port={self._port}", ex);
                }
                self._listener = null;

                try
                {
                    _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                                .CancelGroup($"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Tcp}/{self._port}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                        $"cancel-group-failed port={self._port}", ex);
                }

                _ = Interlocked.Exchange(ref self._state, (int)ListenerState.STOPPED);

                s_logger?.Info(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                    $"stopped port={self._port}");
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                s_logger?.Error(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                    $"stop-error port={self._port} ex={ex.Message}");
            }
            finally
            {
                try
                {
                    self._cts?.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                        $"cts-dispose-ignored port={self._port} reason={ex.GetType().Name}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                        $"cts-dispose-failed port={self._port}", ex);
                }
                self._cts = null;

                _ = Interlocked.Exchange(ref self._stopInitiated, 0);

                if (lockTaken)
                {
                    try
                    {
                        _ = self._lock.Release();
                    }
                    catch (SemaphoreFullException ex)
                    {
                        s_logger?.Warn(
                            $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                            $"lock-release-ignored port={self._port} reason={nameof(SemaphoreFullException)} ex={ex.Message}");
                    }
                    catch (ObjectDisposedException ex)
                    {
                        s_logger?.Warn(
                            $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                            $"lock-release-ignored port={self._port} reason={nameof(ObjectDisposedException)} ex={ex.Message}");
                    }
                    catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                    {
                        s_logger?.Error(
                            $"[NW.{nameof(TcpListenerBase)}:{nameof(SCHEDULE_STOP)}] " +
                            $"lock-release-error port={self._port} ex={ex.Message}");
                    }
                }
            }
        }

        // UnsafeQueueUserWorkItem -> no capture ExecutionContext (more secure,
        // Higher performance because it doesn't copy the context).
        // WHY Unsafe: This is infrastructure code, no flow security context is needed.
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
                try
                {
                    _cancelReg.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"cancel-reg-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"cancel-reg-dispose-failed port={_port}", ex);
                }

                try
                {
                    _cts?.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"cts-cancel-ignored port={_port} reason={ex.GetType().Name}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"cts-cancel-failed port={_port}", ex);
                }

                try
                {
                    _cts?.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"cts-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"cts-dispose-failed port={_port}", ex);
                }

                _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPING);

                try
                {
                    _listener?.Close();
                    _listener?.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"listener-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                        $"listener-dispose-failed port={_port}", ex);
                }
                finally
                {
                    _listener = null;
                }
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                s_logger?.Error(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] " +
                    $"dispose-failed port={_port}", ex);
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            this.STOP_PROCESS_CHANNEL();
            Interlocked.Exchange(ref _processWorker, null)?.Dispose();
            _processChannel = null;

            _lock.Dispose();
        }

        s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] disposed");
    }

    #endregion IDispose
}
