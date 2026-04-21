// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.Configuration;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Internal.Security;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Options;

namespace Nalix.Network.Connections;

/// <summary>
/// Represents a network connection that manages socket communication, stream
/// transformation, and event handling.
/// This is the high-level owner for the socket transport and the per-connection
/// event pipeline.
/// </summary>
public sealed partial class Connection : IConnection, IConnectionErrorTracked
{
    #region Fields

    private static readonly NetworkSocketOptions s_options = ConfigurationManager.Instance.Get<NetworkSocketOptions>();

    private readonly ILogger? _logger;

    private readonly Lock _lock;
    private readonly ConnectionEventArgs _args;
    private int _errorCount;
    private int _closeSignaled;
    private long _bytesSent;
    private long _argsPoolMask; // Bitmask for free/busy status.

    private volatile bool _disposed;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    // Per-connection local pool for packet arguments to avoid global pool contention.
    // Size 8 matches the default MaxPerConnectionPendingPackets.
    private readonly ConnectionEventArgs[] _argsPool;

    private readonly PooledConnectEventContext[] _contextPool;
    private long _contextPoolMask;

    /// <summary>
    /// Tracks the current timeout task in the TimingWheel.
    /// Used for manual reference breaking during Dispose to allow instant GC.
    /// </summary>
    internal Internal.Time.TimingWheel.TimeoutTask? _timeoutTask;

    #endregion Fields

    #region Constructor

    /// <summary>Initializes a new instance of the <see cref="Connection"/> class.</summary>
    /// <param name="socket">The connected socket used for the connection.</param>
    /// <param name="logger">The logger instance for logging connection events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="socket"/> is null.</exception>
    public Connection(Socket socket, ILogger? logger = null)
    {
        _lock = new Lock();
        _disposed = false;
        _logger = logger;

        this.Secret = Bytes32.Zero;
        // Snapshot the remote endpoint up front so the connection can be logged
        // and tracked even before protocol-level events begin.
        this.ID = Snowflake.NewId(SnowflakeType.Session);
        this.NetworkEndpoint = SocketEndpoint.FromEndPoint(socket?.RemoteEndPoint ?? throw new InternalErrorException("Socket does not expose a remote endpoint."));

        _args = new ConnectionEventArgs(this);
        _argsPool = new ConnectionEventArgs[8];
        for (int i = 0; i < _argsPool.Length; i++)
        {
            _argsPool[i] = new ConnectionEventArgs(this)
            {
                OnDisposedCallback = this.ReturnEventArgsSync
            };
        }

        _contextPool = new PooledConnectEventContext[8];
        for (int i = 0; i < _contextPool.Length; i++)
        {
            _contextPool[i] = new PooledConnectEventContext
            {
                OnDisposedCallback = this.ReturnContextSync
            };
        }

        this.Socket = new SocketConnection(socket, logger);

        // Wire the socket-level events into the connection-level callback pipeline.
        this.Socket.SetCallback(this, _args, this.OnCloseEventBridge, OnPostProcessEventBridge, OnProcessEventBridge);

        this.TCP = new SocketTcpTransport(this);
        this.Attributes = ObjectMap<string, object>.Rent();

        _logger?.Trace($"[NW.{nameof(Connection)}] created remote={this.NetworkEndpoint} id={this.ID}");
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc />
    public ISnowflake ID { get; }

    /// <inheritdoc/>
    public IConnection.ITransport TCP { get; }

    /// <inheritdoc/>
    public IConnection.ITransport UDP => this.UdpTransport ?? throw new InternalErrorException("UDP transport has not been created yet.");

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint { get; }

    /// <inheritdoc />
    public IObjectMap<string, object> Attributes { get; }

    /// <inheritdoc />
    public int ErrorCount => _errorCount;

    /// <inheritdoc />
    public long UpTime => this.Socket.Uptime;

    /// <inheritdoc />
    public long LastPingTime => this.Socket.LastPingTime;

    /// <inheritdoc />
    public PermissionLevel Level { get; set; } = PermissionLevel.NONE;

    /// <inheritdoc />
    public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;

    /// <inheritdoc />
    public Bytes32 Secret { get; set; }

    /// <summary>Gets the total number of bytes sent through this connection.</summary>
    /// <returns>The total number of bytes sent.</returns>
    public long BytesSent
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _bytesSent);
    }

    /// <inheritdoc />
    public void IncrementErrorCount()
    {
        int count = Interlocked.Increment(ref _errorCount);
        if (s_options.MaxErrorThreshold > 0 && count >= s_options.MaxErrorThreshold) // SEC-54: Disconnect persistent noisy/malformed connections
        {
            this.Disconnect("Exceeded maximum error threshold.");
        }
    }

    #endregion Properties

    #region Internal

    internal SocketConnection Socket { get; }

    internal SocketUdpTransport? UdpTransport { get; private set; }

    internal SlidingWindow UdpReplayWindow { get; } = new(s_options.UdpReplayWindowSize);

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleasePendingPacket() => this.Socket.OnPacketProcessed();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddBytesSent(int count) => _ = Interlocked.Add(ref _bytesSent, count);
    internal void SetUdpTransport(SocketUdpTransport transport) => this.UdpTransport = transport;

    #endregion Internal

    #region Events

    /// <inheritdoc />

    public event EventHandler<IConnectEventArgs> OnCloseEvent
    {
        add => _onCloseEvent += value;
        remove => _onCloseEvent -= value;
    }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs> OnProcessEvent
    {
        add => _onProcessEvent += value;
        remove => _onProcessEvent -= value;
    }

    /// <inheritdoc />
    public event EventHandler<IConnectEventArgs> OnPostProcessEvent
    {
        add => _onPostProcessEvent += value;
        remove => _onPostProcessEvent -= value;
    }

    #endregion Events

    #region Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Close(bool force = false)
    {
        if (_disposed)
        {
            return;
        }

        // Route close through the bridge so the same high-priority callback path
        // is used everywhere.
        this.OnCloseEventBridge(this, new ConnectionEventArgs(this));

#if DEBUG
        _logger?.Debug($"[NW.{nameof(Connection)}:{this.Close}] close request id={this.ID} remote={this.NetworkEndpoint}");
#endif
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Disconnect(string? reason = null) => this.Close(force: true);

    /// <summary>
    /// Manually triggers the receive-path process callback for a given buffer lease.
    /// This is used exclusively for testing to simulate incoming packets without a real socket.
    /// </summary>
    /// <param name="lease">The buffer lease carrying the simulated packet payload.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void InjectIncoming(BufferLease lease)
    {
        this.Socket.IncrementPendingCallbacks();

        ConnectionEventArgs args = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Get<ConnectionEventArgs>();
        args.Initialize(lease, this);

        if (!Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args, releasePendingPacketOnCompletion: true))
        {
            this.ReleasePendingPacket();
            args.Dispose();
        }
    }

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        try
        {
            this.Secret = Bytes32.Zero;

            // Return pooled metadata first so the connection does not keep
            // borrowed state alive after disposal begins.
            this.Attributes.Return();

            this.Disconnect();

            // High-Performance Cleanup: Break the TimingWheel reference chain instantly.
            // This allows the GC to collect the Connection immediately instead of 
            // waiting for the 102s wheel rotation.
            Internal.Time.TimingWheel.TimeoutTask? task = _timeoutTask;
            if (task is not null)
            {
                task.Conn = null;
                _timeoutTask = null;
            }

            this.Socket.Dispose();

            if (this.UdpTransport != null)
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return(this.UdpTransport);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"[NW.{nameof(Connection)}:{nameof(this.Dispose)}] dispose-error msg={ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    #endregion Dispose Pattern

    #region Internal Pooling

    /// <summary>
    /// Acquires an EventArgs from the connection's local pool for packet processing.
    /// Returns null if the local pool is exhausted (throttle reached).
    /// </summary>
    internal ConnectionEventArgs? AcquireEventArgs()
    {
        for (int i = 0; i < 8; i++)
        {
            long bit = 1L << i;
            if ((Interlocked.Read(ref _argsPoolMask) & bit) == 0)
            {
                if ((Interlocked.Or(ref _argsPoolMask, bit) & bit) == 0)
                {
                    return _argsPool[i];
                }
            }
        }
        return null;
    }

    private void ReturnEventArgsSync(ConnectionEventArgs args)
    {
        for (int i = 0; i < 8; i++)
        {
            if (ReferenceEquals(_argsPool[i], args))
            {
                long bit = 1L << i;
                _argsPool[i].ResetForPool();
                _ = Interlocked.And(ref _argsPoolMask, ~bit);
                return;
            }
        }
    }

    /// <summary>
    /// Acquires a transition context from the connection's local pool.
    /// Used by AsyncCallback to execute packet handoffs without global pooling.
    /// </summary>
    internal PooledConnectEventContext? AcquireContext()
    {
        for (int i = 0; i < 8; i++)
        {
            long bit = 1L << i;
            if ((Interlocked.Read(ref _contextPoolMask) & bit) == 0)
            {
                if ((Interlocked.Or(ref _contextPoolMask, bit) & bit) == 0)
                {
                    return _contextPool[i];
                }
            }
        }
        return null;
    }

    private void ReturnContextSync(PooledConnectEventContext context)
    {
        for (int i = 0; i < 8; i++)
        {
            if (ReferenceEquals(_contextPool[i], context))
            {
                long bit = 1L << i;
                _contextPool[i].ResetForPool();
                _ = Interlocked.And(ref _contextPoolMask, ~bit);
                return;
            }
        }
    }

    #endregion Internal Pooling

    #region Event Bridges

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void OnCloseEventBridge(object? sender, IConnectEventArgs e)
    {
        if (Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        // Close events bypass backpressure because cleanup must never be delayed.
        if (!Internal.Transport.AsyncCallback.InvokeHighPriority(OnCloseEventDispatchBridge, this, e))
        {
            e.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void OnProcessEventBridge(object? sender, IConnectEventArgs e)
    {
        if (e is null)
        {
            return;
        }

        if (sender is not Connection self)
        {
            e.Dispose();
            return;
        }

        try
        {
            self._onProcessEvent?.Invoke(self, e);
        }
        finally
        {
            e.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void OnPostProcessEventBridge(object? sender, IConnectEventArgs e)
    {
        if (e is null)
        {
            return;
        }

        if (sender is not Connection self)
        {
            e.Dispose();
            return;
        }

        try
        {
            self._onPostProcessEvent?.Invoke(self, e);
        }
        finally
        {
            e.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void OnCloseEventDispatchBridge(object? sender, IConnectEventArgs e)
    {
        if (e is null)
        {
            return;
        }

        if (sender is not Connection self)
        {
            e.Dispose();
            return;
        }

        try
        {
            self._onCloseEvent?.Invoke(self, e);
        }
        finally
        {
            e.Dispose();
        }
    }

    #endregion Event Bridges
}
