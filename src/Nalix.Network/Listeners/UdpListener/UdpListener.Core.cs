// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
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

    private static readonly NetworkSocketOptions s_options;
    private static readonly ConnectionLimitOptions s_connectionLimitOptions;
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static UdpListenerBase()
    {
        s_options = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        s_connectionLimitOptions = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
        s_connectionLimitOptions.Validate();
    }

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

        _hub = hub;
        _port = port;
        _protocol = protocol;
        _lock = new SemaphoreSlim(1, 1);
        _state = (int)ListenerState.STOPPED;
        _rateLimiter = new(s_connectionLimitOptions.MaxPacketPerSecond);

        // Default to IPv4 any-address; Initialize() may switch to IPv6 based on config.
        _anyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        s_logger?.Debug($"[NW.{nameof(UdpListenerBase)}] created port={_port} protocol={protocol.GetType().Name}");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class using the configured port.
    /// </summary>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    /// <param name="hub">The connection hub for managing active connections.</param>
    [DebuggerStepThrough]
    protected UdpListenerBase(IProtocol protocol, IConnectionHub hub) : this(s_options.Port, protocol, hub)
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
                s_logger?.Debug(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                    $"cts-dispose-ignored port={_port} reason={ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                s_logger?.Warn(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                    $"cts-dispose-failed port={_port}", ex);
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
                s_logger?.Debug(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                    $"socket-dispose-ignored port={_port} reason={ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                s_logger?.Warn(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] " +
                    $"socket-dispose-failed port={_port}", ex);
            }

            _socket = null;
            _lock.Dispose();

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }

        s_logger?.Debug($"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] disposed port={_port}");
    }

    #endregion IDisposable


}
