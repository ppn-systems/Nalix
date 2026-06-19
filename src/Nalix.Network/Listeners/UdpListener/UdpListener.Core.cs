// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    #region Enums

    /// <summary>
    /// Represents the lifecycle state of the UDP listener.
    /// Transitions follow: STOPPED → STARTING → RUNNING → STOPPING → STOPPED.
    /// </summary>
    private enum ListenerState
    {
        STOPPED = 0,
        STARTING = 1,
        RUNNING = 2,
        STOPPING = 3
    }

    #endregion Enums

    #region Fields

    private readonly NetworkSocketOptions _options;
    private readonly DatagramGuardOptions _datagramGuardOptions;
    private readonly ConnectionGuardOptions _connectionGuardOptions;

    private readonly ushort _port;
    private readonly SemaphoreSlim _lock;
    private readonly IConnectionHub _hub;
    private readonly ITaskManager _taskManager;
    private readonly DatagramGuard _rateLimiter;

    private Socket? _socket;
    private EndPoint _anyEndPoint;
    private CancellationTokenSource? _cts;
    private CancellationToken _cancellationToken;
    private IWorkerHandle[]? _receiveWorkers;

    private int _state;
    private int _isDisposed;
    private int _stopInitiated;

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    protected IProtocol Protocol { get; }

    /// <summary>
    /// Gets the underlying listener socket used for UDP datagram operations.
    /// </summary>
    /// <remarks>
    /// This socket is shared between all connections served by this listener.
    /// Derived classes may use it to send replies to remote endpoints.
    /// Returns <c>null</c> if the socket has not been initialized or has been disposed.
    /// </remarks>
    protected Socket? ListenerSocket => _socket;

    /// <summary>
    /// Gets a value indicating whether the UDP listener is currently running and listening for datagrams.
    /// </summary>
    public bool IsListening => this.State == ListenerState.RUNNING;

    /// <summary>
    /// Gets the current lifecycle state of the listener (thread-safe volatile read).
    /// </summary>
    private ListenerState State => (ListenerState)Volatile.Read(ref _state);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class with the specified port and protocol.
    /// </summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    /// <param name="taskManager">The task manager.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="protocol"/> is <c>null</c>.</exception>
    [DebuggerStepThrough]
    protected UdpListenerBase(ushort port, IProtocol protocol, IConnectionHub hub, ITaskManager taskManager)
    {
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        ArgumentNullException.ThrowIfNull(hub, nameof(hub));

        _options = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        _datagramGuardOptions = ConfigurationManager.Instance.Get<DatagramGuardOptions>();
        _connectionGuardOptions = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();

        _options.Validate();
        _datagramGuardOptions.Validate();
        _connectionGuardOptions.Validate();

        _hub = hub;
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _port = port;
        this.Protocol = protocol;
        _lock = new SemaphoreSlim(1, 1);
        _state = (int)ListenerState.STOPPED;
        _rateLimiter = new(_datagramGuardOptions, _connectionGuardOptions);

        // Default to IPv4 any-address; Initialize() may switch to IPv6 based on config.
        _anyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            string protocolType = protocol.GetType().Name;
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.with:UnknownMethod", $"created port={_port} protocol-type={protocolType}"));
            }
            ;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class using the configured port.
    /// </summary>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    /// <param name="taskManager">The task manager.</param>
    [DebuggerStepThrough]
    protected UdpListenerBase(IProtocol protocol, IConnectionHub hub, ITaskManager taskManager) : this(ConfigurationManager.Instance.Get<NetworkSocketOptions>().Port, protocol, hub, taskManager)
    {
    }

    #endregion Constructors

    #region IDisposable

    /// <inheritdoc/>
    [DebuggerStepThrough]
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    [DebuggerStepThrough]
    protected virtual void Dispose(bool disposing)
    {
        // Atomic check-and-set: 0 -> 1. Prevents double-dispose.
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (disposing)
        {
            this.Deactivate();

            IWorkerHandle[]? receiveWorkers = Interlocked.Exchange(ref _receiveWorkers, null);
            if (receiveWorkers != null)
            {
                foreach (IWorkerHandle? worker in receiveWorkers)
                {
                    worker?.Dispose();
                }
            }

            try
            {
                _cts?.Cancel();
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

            _cts = null;
            _cancellationToken = default;

            try
            {
                _socket?.Close();
                _socket?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", $"socket-dispose-ignored port={_port} exception-type={ex.GetType().Name}", ex));
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.using:Dispose", $"socket-dispose-failed port={_port}", ex));
                }
            }

            _socket = null;
            _lock.Dispose();

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.using:Dispose", $"disposed port={_port}"));
        }
    }

    #endregion IDisposable
}
