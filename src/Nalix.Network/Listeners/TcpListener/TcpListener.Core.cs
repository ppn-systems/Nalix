// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Internal.Time;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Nalix.Network.Listeners.Tcp;

[SkipLocalsInit]
[DebuggerNonUserCode]
public abstract partial class TcpListenerBase : IListener
{
    #region Fields

    private readonly ushort _port;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _lock;
    private readonly IConnectionHub _hub;
    private readonly IProtocol _protocol;

    private readonly NetworkSocketOptions _config;
    private readonly ProxyProtocolOptions _proxyConfig;
    private readonly TimingWheel _timing;
    private readonly ObjectPoolManager _pool;
    private readonly ConnectionGuard _limiter;
    private int _state;
    private int _isDisposed;
    private int _stopInitiated;
    private int _pendingProxyConnections;

    private Socket? _listener;
    private CancellationTokenSource? _cts;
    private IWorkerHandle[]? _acceptWorkers;
    private CancellationToken _cancellationToken;
    private CancellationTokenRegistration _cancelReg;

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

        // Fetch infrastructure instances via InstanceManager for proper test isolation
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        _config = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        _proxyConfig = ConfigurationManager.Instance.Get<ProxyProtocolOptions>();

        _timing = InstanceManager.Instance.GetOrCreateInstance<TimingWheel>();
        _pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
        _limiter = InstanceManager.Instance.GetOrCreateInstance<ConnectionGuard>();

        _config.Validate();

        _lock = new SemaphoreSlim(1, 1);

        PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();
        options.Validate();

        // Configure object pools for accept contexts and socket async event args based on the provided options.
        _ = _pool.SetMaxCapacity<PooledAcceptContext>(options.AcceptContextCapacity);
        _ = _pool.SetMaxCapacity<PooledSocketAsyncEventArgs>(options.SocketArgsCapacity);

        // Preallocate objects in the pools to improve performance and reduce latency during runtime.
        _ = _pool.Prealloc<PooledAcceptContext>(options.AcceptContextPreallocate);
        _ = _pool.Prealloc<PooledSocketAsyncEventArgs>(options.SocketArgsPreallocate);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    [DebuggerStepThrough]
    protected TcpListenerBase(IProtocol protocol, IConnectionHub hub) : this(ConfigurationManager.Instance.Get<NetworkSocketOptions>().Port, protocol, hub)
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

        async Task cb(object? state)
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
                    await (self._cts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                }
                catch (ObjectDisposedException ex)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] cts-cancel-ignored port={Port} reason={ExceptionType}", self._port, ex.GetType().Name);
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] cts-cancel-failed port={Port}", self._port);
                    }
                }

                // Close socket server -> AcceptAsync will throw SocketException -> loop exits.
                try
                {
                    self._listener?.Close();
                }
                catch (ObjectDisposedException ex)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] listener-close-ignored port={Port} reason={ExceptionType}", self._port, ex.GetType().Name);
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] listener-close-failed port={Port}", self._port);
                    }
                }
                self._listener = null;

                try
                {
                    IWorkerHandle[]? acceptWorkers = Interlocked.Exchange(ref self._acceptWorkers, null);
                    if (acceptWorkers != null)
                    {
                        foreach (IWorkerHandle? worker in acceptWorkers)
                        {
                            worker?.Dispose();
                        }
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] cancel-group-failed port={Port}", self._port);
                    }
                }

                _ = Interlocked.Exchange(ref self._state, (int)ListenerState.STOPPED);

                if (_logger != null && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("[NW.TcpListenerBase:SCHEDULE_STOP] stopped port={Port}", self._port);
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] stop-error port={Port}", self._port);
                }
            }
            finally
            {
                try
                {
                    self._cts?.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] cts-dispose-ignored port={Port} reason={ExceptionType}", self._port, ex.GetType().Name);
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] cts-dispose-failed port={Port}", self._port);
                    }
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
                        if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                        {
                            _logger.LogWarning(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] lock-release-ignored port={Port} reason=SemaphoreFullException", self._port);
                        }
                    }
                    catch (ObjectDisposedException ex)
                    {
                        if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                        {
                            _logger.LogWarning(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] lock-release-ignored port={Port} reason=ObjectDisposedException", self._port);
                        }
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError(ex, "[NW.TcpListenerBase:SCHEDULE_STOP] lock-release-error port={Port}", self._port);
                        }
                    }
                }
            }
        }

        // Use Task.Run to properly handle async state machines and exceptions,
        // avoiding ThreadPool starvation or unobserved exception crashes caused by async void.
        _ = Task.Run(() => cb(this));
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
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "[NW.TcpListenerBase:Dispose] cancel-reg-dispose-ignored port={Port} reason={ExceptionType}", _port, ex.GetType().Name);
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:Dispose] cancel-reg-dispose-failed port={Port}", _port);
                    }
                }

                try
                {
                    _cts?.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "[NW.TcpListenerBase:Dispose] cts-cancel-ignored port={Port} reason={ExceptionType}", _port, ex.GetType().Name);
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:Dispose] cts-cancel-failed port={Port}", _port);
                    }
                }

                try
                {
                    _cts?.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "[NW.TcpListenerBase:Dispose] cts-dispose-ignored port={Port} reason={ExceptionType}", _port, ex.GetType().Name);
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:Dispose] cts-dispose-failed port={Port}", _port);
                    }
                }

                _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPING);

                try
                {
                    _listener?.Close();
                    _listener?.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(ex, "[NW.TcpListenerBase:Dispose] listener-dispose-ignored port={Port} reason={ExceptionType}", _port, ex.GetType().Name);
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(ex, "[NW.TcpListenerBase:Dispose] listener-dispose-failed port={Port}", _port);
                    }
                }
                finally
                {
                    _listener = null;
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "[NW.TcpListenerBase:Dispose] dispose-failed port={Port}", _port);
                }
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);

            IWorkerHandle[]? acceptWorkers = Interlocked.Exchange(ref _acceptWorkers, null);
            if (acceptWorkers != null)
            {
                foreach (IWorkerHandle? worker in acceptWorkers)
                {
                    worker?.Dispose();
                }
            }

            this.STOP_PROCESS_CHANNEL();
            _processWorker?.Dispose();
            _processWorker = null;
            _processChannel = null;

            _lock.Dispose();
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("[NW.TcpListenerBase:Dispose] disposed");
        }
    }

    #endregion IDispose
}
