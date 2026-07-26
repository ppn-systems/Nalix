// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Initialization;
using Nalix.Network.Internal.Time;
using Nalix.Network.Options;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Nalix.Network.Listeners.Tcp;

[SkipLocalsInit]
[DebuggerNonUserCode]
public abstract partial class TcpListenerBase : IListener
{
    #region Fields

    private readonly ushort _port;

    private readonly SemaphoreSlim _lock;
    private readonly IConnectionHub _hub;
    private readonly IProtocol _protocol;

    private readonly NetworkSocketOptions _config;
    private readonly ProxyProtocolOptions _proxyConfig;
    private readonly TimingWheel _timing;
    private readonly ObjectPoolManager _pool;
    private readonly IConnectionGuard _limiter;

    private int _state;
    private int _isDisposed;
    private int _stopInitiated;
    private int _pendingProxyConnections;

    private Socket? _listener;
    private CancellationTokenSource? _cts;
    private IWorkerHandle[]? _acceptWorkers;
    private CancellationToken _cancellationToken;
    private CancellationTokenRegistration _cancelReg;

    /// <summary>
    /// Gets the connection guard used to limit the number of concurrent connections.
    /// </summary>
    protected internal IConnectionGuard Limiter => _limiter;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    private ListenerState State => (ListenerState)Volatile.Read(ref _state);

    /// <summary>
    /// Gets the current number of connections queued in the accept processing channel.
    /// </summary>
    public int ProcessChannelCount => _processChannel?.Reader.CanCount == true ? _processChannel.Reader.Count : 0;

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
    /// <param name="guard">The connection guard.</param>
    [DebuggerStepThrough]
    protected TcpListenerBase(ushort port, IProtocol protocol, IConnectionHub hub, IConnectionGuard guard)
    {
        ArgumentNullException.ThrowIfNull(hub, nameof(hub));
        ArgumentNullException.ThrowIfNull(guard, nameof(guard));
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));

        _isDisposed = 0;

        _hub = hub;
        _port = port;
        _protocol = protocol;
        _state = (int)ListenerState.STOPPED;

        // Fetch infrastructure instances via InstanceManager for proper test isolation

        _config = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        _proxyConfig = ConfigurationManager.Instance.Get<ProxyProtocolOptions>();

        _timing = InstanceManager.Instance.GetOrCreateInstance<TimingWheel>();
        _pool = ObjectPoolManager.Shared;
        _limiter = guard;

        _config.Validate();

        _lock = new SemaphoreSlim(1, 1);

        _ = nameof(NetworkPoolInitializer);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpListenerBase"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    /// <param name="guard">The connection guard.</param>
    [DebuggerStepThrough]
    protected TcpListenerBase(IProtocol protocol, IConnectionHub hub, IConnectionGuard guard)
        : this(ConfigurationManager.Instance.Get<NetworkSocketOptions>().Port, protocol, hub, guard)
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

        static async Task cb(object? state)
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
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:cb", $"cts-cancel-ignored port={self._port} exception-type={ex.GetType().Name}", ex));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:cb", $"cts-cancel-failed port={self._port}", ex));
                    }
                }

                // Close socket server -> AcceptAsync will throw SocketException -> loop exits.
                try
                {
                    self._listener?.Close();
                }
                catch (ObjectDisposedException ex)
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:cb", $"listener-close-ignored port={self._port} exception-type={ex.GetType().Name}", ex));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:cb", $"listener-close-failed port={self._port}", ex));
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
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:cb", $"cancel-group-failed port={self._port}", ex));
                    }
                }

                _ = Interlocked.Exchange(ref self._state, (int)ListenerState.STOPPED);

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Listeners.Stopped))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Listeners.Stopped, new DiagnosticLog("NW.using:cb", $" port={self._port}"));
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.LoopFaulted, new DiagnosticLog("NW.using:cb", $"stop-error port={self._port}", ex));
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
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:cb", $"cts-dispose-ignored port={self._port} exception-type={ex.GetType().Name}", ex));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:cb", $"cts-dispose-failed port={self._port}", ex));
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
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                        {
                            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                            {
                                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:cb", $"lock-release-ignored port={self._port} reason={"SemaphoreFullException"}", ex));
                            }
                            ;
                        }
                    }
                    catch (ObjectDisposedException ex)
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                        {
                            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                            {
                                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:cb", $"lock-release-ignored port={self._port} reason={"ObjectDisposedException"}", ex));
                            }
                            ;
                        }
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
                        {
                            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
                            {
                                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.LoopFaulted, new DiagnosticLog("NW.using:cb", $"lock-release-error port={self._port}", ex));
                            }
                            ;
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
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", $"cancel-reg-dispose-ignored port={_port} exception-type={ex.GetType().Name}", ex));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:Dispose", $"cancel-reg-dispose-failed port={_port}", ex));
                    }
                }

                try
                {
                    _cts?.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", $"cts-cancel-ignored port={_port} exception-type={ex.GetType().Name}", ex));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:Dispose", $"cts-cancel-failed port={_port}", ex));
                    }
                }

                try
                {
                    _cts?.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", $"cts-dispose-ignored port={_port} exception-type={ex.GetType().Name}", ex));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:Dispose", $"cts-dispose-failed port={_port}", ex));
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
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", $"listener-dispose-ignored port={_port} exception-type={ex.GetType().Name}", ex));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:Dispose", $"listener-dispose-failed port={_port}", ex));
                    }
                }
                finally
                {
                    _listener = null;
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.LoopFaulted, new DiagnosticLog("NW.using:Dispose", $"dispose-failed port={_port}", ex));
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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", $"TcpListenerBase disposed port={_port}"));
        }
    }

    #endregion IDispose
}
