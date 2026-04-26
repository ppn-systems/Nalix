// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Options;
using Nalix.Network.RateLimiting;

#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA2254 // Template should be a static expression

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
    private readonly ConnectionLimitOptions _connectionLimitOptions;
    private readonly DatagramGuardOptions _datagramGuardOptions;
    private readonly ILogger? _logger;

    private readonly ushort _port;
    private readonly IProtocol _protocol;
    private readonly SemaphoreSlim _lock;
    private readonly IConnectionHub _hub;
    private readonly DatagramGuard _rateLimiter;

    private Socket? _socket;
    private EndPoint _anyEndPoint;
    private CancellationTokenSource? _cts;
    private CancellationToken _cancellationToken;

    private int _state;
    private int _isDisposed;
    private int _stopInitiated;

    // Diagnostic counters — grouped for clarity, accessed via Interlocked.
    private long _rxPackets;
    private long _rxBytes;
    private long _dropShort;
    private long _dropUnauth;
    private long _dropUnknown;
    private long _recvErrors;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current lifecycle state of the listener (thread-safe volatile read).
    /// </summary>
    private ListenerState State => (ListenerState)Volatile.Read(ref _state);

    /// <summary>
    /// Gets a value indicating whether the UDP listener is currently running and listening for datagrams.
    /// </summary>
    public bool IsListening => this.State == ListenerState.RUNNING;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class with the specified port and protocol.
    /// </summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="protocol"/> is <c>null</c>.</exception>
    [DebuggerStepThrough]
    protected UdpListenerBase(ushort port, IProtocol protocol, IConnectionHub hub)
    {
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        ArgumentNullException.ThrowIfNull(hub, nameof(hub));

        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        _options = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        _datagramGuardOptions = ConfigurationManager.Instance.Get<DatagramGuardOptions>();
        _connectionLimitOptions = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();

        _datagramGuardOptions.Validate();
        _connectionLimitOptions.Validate();

        _hub = hub;
        _port = port;
        _protocol = protocol;
        _lock = new SemaphoreSlim(1, 1);
        _state = (int)ListenerState.STOPPED;
        _rateLimiter = new(
            _connectionLimitOptions.MaxPacketPerSecond,
            _datagramGuardOptions.IPv4Windows,
            _datagramGuardOptions.IPv6Windows,
            _datagramGuardOptions.CleanupInterval,
            _datagramGuardOptions.IdleTimeout,
            _datagramGuardOptions.IPv4Capacity,
            _datagramGuardOptions.IPv6Capacity);

        // Default to IPv4 any-address; Initialize() may switch to IPv6 based on config.
        _anyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug($"[NW.{nameof(UdpListenerBase)}] created port={_port} protocol={protocol.GetType().Name}");
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class using the configured port.
    /// </summary>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    [DebuggerStepThrough]
    protected UdpListenerBase(IProtocol protocol, IConnectionHub hub) : this(ConfigurationManager.Instance.Get<NetworkSocketOptions>().Port, protocol, hub)
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

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                        $"cts-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex,
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                        $"cts-dispose-failed port={_port}");
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
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                        $"socket-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex,
                        $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                        $"socket-dispose-failed port={_port}");
                }
            }

            _socket = null;
            _lock.Dispose();

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug($"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] disposed port={_port}");
        }
    }

    #endregion IDisposable


}
