// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Internal.Time;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;

#pragma warning disable IDE0079
#pragma warning disable CA2213

namespace Nalix.Network.Listeners.Web;

/// <summary>
/// Provides a base implementation for a WebSocket listener using <see cref="System.Net.HttpListener"/>.
/// </summary>
[SkipLocalsInit]
[DebuggerNonUserCode]
public abstract partial class WebSocketListenerBase : IListener
{
    #region Fields

    private readonly ushort _port;
    private readonly string _path;
    private readonly IProtocol _protocol;
    private readonly SemaphoreSlim _lock;
    private readonly IConnectionHub _hub;
    private readonly TimingWheel _timing;
    private readonly ConnectionGuard _limiter;
    private readonly NetworkWebSocketOptions _config;
    private readonly ForwardedHeadersOptions _forwardedConfig;

    private int _state;
    private int _isDisposed;
    private int _stopInitiated;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private CancellationTokenRegistration _cancelReg;
    private IWorkerHandle[]? _acceptWorkers;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    private ListenerState State => (ListenerState)Volatile.Read(ref _state);

    /// <summary>
    /// Gets the number of connections currently in the accept queue.
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
    /// Initializes a new instance of the <see cref="WebSocketListenerBase"/> class using the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="path">The HTTP path prefix to listen on.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    [DebuggerStepThrough]
    protected WebSocketListenerBase(ushort port, string path, IProtocol protocol, IConnectionHub hub)
    {
        ArgumentNullException.ThrowIfNull(hub, nameof(hub));
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));

        _isDisposed = 0;
        _hub = hub;
        _port = port;
        _path = path;
        _protocol = protocol;

        _state = (int)ListenerState.STOPPED;


        _timing = InstanceManager.Instance.GetOrCreateInstance<TimingWheel>();
        _config = ConfigurationManager.Instance.Get<NetworkWebSocketOptions>();
        _forwardedConfig = ConfigurationManager.Instance.Get<ForwardedHeadersOptions>();
        _limiter = InstanceManager.Instance.GetOrCreateInstance<ConnectionGuard>();

        _config.Validate();

        _lock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketListenerBase"/> class using configuration defaults.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    [DebuggerStepThrough]
    protected WebSocketListenerBase(IProtocol protocol, IConnectionHub hub)
        : this(ConfigurationManager.Instance.Get<NetworkWebSocketOptions>().Port,
               ConfigurationManager.Instance.Get<NetworkWebSocketOptions>().Path,
               protocol, hub)
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
            if (state is not WebSocketListenerBase self)
            {
                return;
            }

            bool lockTaken = false;
            try
            {
                await self._lock.WaitAsync().ConfigureAwait(false);
                lockTaken = true;

                if (Volatile.Read(ref self._isDisposed) != 0)
                {
                    return;
                }

                try { await (self._cts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false); }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

                try
                {
                    if (self._listener != null && self._listener.IsListening)
                    {
                        self._listener.Stop();
                        self._listener.Close();
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
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
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

                _ = Interlocked.Exchange(ref self._state, (int)ListenerState.STOPPED);

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.using:cb", $"stopped port={self._port}"));
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
            finally
            {
                try { self._cts?.Dispose(); }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
                self._cts = null;

                _ = Interlocked.Exchange(ref self._stopInitiated, 0);

                if (lockTaken)
                {
                    try { _ = self._lock.Release(); }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
                }
            }
        }

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
    [DebuggerStepThrough]
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (disposing)
        {
            this.Deactivate();

            try { _cancelReg.Dispose(); } catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
            try { _cts?.Cancel(); } catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
            try { _cts?.Dispose(); } catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPING);

            try
            {
                if (_listener != null && _listener.IsListening)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
            finally
            {
                _listener = null;
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
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", "disposed"));
        }
    }

    #endregion IDispose
}
