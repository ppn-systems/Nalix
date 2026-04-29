// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.Memory;
using Nalix.Environment.Configuration;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
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

    private static readonly ConnectionLimitOptions s_options = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();

    private readonly ILogger? _logger;

    private readonly Lock _lock;
    private readonly ConnectionEventArgs _args;
    private int _errorCount;
    private int _closeSignaled;
    private long _bytesSent;
    private long _argsPoolMask; // Bitmask for free/busy status.
    private long _contextPoolMask;
    private int _disposeState; // 0=Active, 1=Closing(Event running), 2=Disposed
    private int _isDispatchingClose; // 0=no, 1=yes

    private IObjectMap<string, object>? _attributes;
    private IConnection.ITransport? _tcp;
    private SlidingWindow? _udpReplayWindow;

    private volatile bool _disposed;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    // Per-connection local pool for packet arguments to avoid global pool contention.
    // Size 8 matches the default MaxPerConnectionPendingPackets.
    private ConnectionEventArgs[]? _argsPool;

    private PooledConnectEventContext[]? _contextPool;

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

        this.Socket = new SocketConnection(socket, logger);

        // Wire the socket-level events into the connection-level callback pipeline.
        this.Socket.SetCallback(this, _args, this.OnCloseEventBridge, OnPostProcessEventBridge, OnProcessEventBridge);

        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"[NW.{nameof(Connection)}] created remote={this.NetworkEndpoint} id={this.ID}");
        }
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc />
    public ISnowflake ID { get; }

    /// <inheritdoc/>
    public IConnection.ITransport TCP => _tcp ??= new SocketTcpTransport(this);

    /// <inheritdoc/>
    public IConnection.ITransport UDP => this.UdpTransport ?? throw new InternalErrorException("UDP transport has not been created yet.");

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint { get; }

    /// <inheritdoc />
    public IObjectMap<string, object> Attributes => _attributes ??= ObjectMap<string, object>.Rent();

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

    /// <inheritdoc />
    public int TimeoutVersion { get; set; }

    /// <inheritdoc />
    public bool IsRegisteredInWheel { get; set; }

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

        // SEC-54: Disconnect persistent noisy/malformed connections
        if (s_options.MaxErrorThreshold > 0 && count >= s_options.MaxErrorThreshold)
        {
            this.Disconnect("Exceeded maximum error threshold.");
        }
    }

    #endregion Properties

    #region Internal

    internal SocketConnection Socket { get; }

    internal SocketUdpTransport? UdpTransport { get; private set; }

    internal SlidingWindow UdpReplayWindow => _udpReplayWindow ??= new(s_options.UdpReplayWindowSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleasePendingPacket() => this.Socket.OnPacketProcessed();

#if DEBUG
    /// <summary>
    /// Injects a packet directly into the process pipeline for testing.
    /// This bypasses the socket receive loop but still triggers AsyncCallback
    /// and respects the per-connection throttle.
    /// </summary>
    internal void InjectIncoming(BufferLease lease)
    {
        this.Socket.IncrementPendingCallbacks();
        ConnectionEventArgs args = this.AcquireEventArgs() ?? new ConnectionEventArgs(this);
        args.Initialize(lease, this);

        if (!Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args, releasePendingPacketOnCompletion: true))
        {
            this.ReleasePendingPacket();
            args.Dispose();
            lease.Dispose();
        }
    }
#endif

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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Disconnect(string? reason = null)
    {
#if DEBUG
        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug($"[NW.{nameof(Connection)}:{nameof(this.Disconnect)}] " +
                           $"disconnect request id={this.ID} remote={this.NetworkEndpoint} reason={reason}");
        }
#endif

        this.Dispose();
    }

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        // Guard against recursive calls or concurrent disposal.
        // Only the first thread that moves state from 0 to 1 gets to trigger the events.
        int previousState = Interlocked.CompareExchange(ref _disposeState, 1, 0);

        if (previousState == 0)
        {
            // We are the primary disposer.
            bool signaledHere = false;
            try
            {
                // Signal that we are closing but NOT yet fully disposed.
                // This allows event handlers (like session persistence) to still read attributes.
                if (Interlocked.Exchange(ref _closeSignaled, 1) == 0)
                {
                    signaledHere = true;
                    if (_onCloseEvent != null)
                    {
                        ConnectionEventArgs closeArgs = new(this);
                        try
                        {
                            Delegate[] handlers = _onCloseEvent.GetInvocationList();
                            foreach (EventHandler<IConnectEventArgs> handler in handlers)
                            {
                                try
                                {
                                    handler(this, closeArgs);
                                }
                                catch (Exception handlerEx) when (ExceptionClassifier.IsNonFatal(handlerEx))
                                {
                                    if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                                    {
                                        _logger.LogError(handlerEx, $"[NW.{nameof(Connection)}:{nameof(this.Dispose)}] close-handler-error");
                                    }
                                }
                            }
                        }
                        finally
                        {
                            closeArgs.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, $"[NW.{nameof(Connection)}:{nameof(this.Dispose)}] close-event-error msg={ex.Message}");
                }
            }
            finally
            {
                // Now that all handlers have finished, we can proceed to the destructive phase.
                // But only if we are the ones who signaled the close AND there is no bridge dispatch running.
                // If a bridge dispatch is running, it will handle cleanup in its own finally block.
                if (signaledHere && Volatile.Read(ref _isDispatchingClose) == 0)
                {
                    this.PerformDestructiveCleanup();
                }
            }
            return;
        }

        // If we are already in state 1 (Closing) or 2 (Disposed), we just return.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
    private void PerformDestructiveCleanup()
    {
        lock (_lock)
        {
            if (Volatile.Read(ref _disposeState) == 2)
            {
                return;
            }

            // Important: we don't set _disposed = true until the end,
            // but we must mark state as 2 immediately to prevent concurrent cleanup.
            Volatile.Write(ref _disposeState, 2);
        }

        try
        {
            this.Secret = Bytes32.Zero;

            try
            {
                // Return pooled metadata first so the connection does not keep
                // borrowed state alive after disposal begins.
                _attributes?.Return();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "attributes"); }
            _attributes = null;

            // High-Performance Cleanup: Break the TimingWheel reference chain instantly.
            // This allows the GC to collect the Connection immediately instead of 
            // waiting for the 102s wheel rotation.
            Internal.Time.TimingWheel.TimeoutTask? task = _timeoutTask;
            if (task is not null)
            {
                task.Conn = null;
                _timeoutTask = null;
            }

            try { this.Socket.Dispose(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "socket"); }

            try { _args.Dispose(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "args"); }

            try
            {
                if (this.UdpTransport != null)
                {
                    InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                            .Return(this.UdpTransport);
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "udptransport"); }

            try
            {
                // Return local pooled objects to global pool to prevent "leak" when connection is destroyed.
                // Without this, every connection "steals" 8 args and 8 contexts from the global pool forever.
                ConnectionEventArgs[]? argsPool = Interlocked.Exchange(ref _argsPool, null);
                if (argsPool != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        argsPool[i]?.Dispose();
                    }
                    System.Buffers.ArrayPool<ConnectionEventArgs>.Shared.Return(argsPool, clearArray: true);
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "argspool"); }

            try
            {
                PooledConnectEventContext[]? ctxPool = Interlocked.Exchange(ref _contextPool, null);
                if (ctxPool != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        ctxPool[i]?.Dispose();
                    }
                    System.Buffers.ArrayPool<PooledConnectEventContext>.Shared.Return(ctxPool, clearArray: true);
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "contextpool"); }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LOG_ERROR(ex, "general");
        }
        finally
        {
            _disposed = true;
        }

        GC.SuppressFinalize(this);

        void LOG_ERROR(Exception ex, string component)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(Connection)}:{nameof(this.Dispose)}] {component}-dispose-error msg={ex.Message}");
            }
        }
    }

    #endregion Dispose Pattern

    #region Internal Pooling

    /// <summary>
    /// Acquires an EventArgs from the connection's local pool for packet processing.
    /// Returns null if the local pool is exhausted (throttle reached).
    /// </summary>
    internal ConnectionEventArgs? AcquireEventArgs()
    {
        if (_argsPool == null)
        {
            lock (this)
            {
                if (_argsPool == null)
                {
                    _argsPool = System.Buffers.ArrayPool<ConnectionEventArgs>.Shared.Rent(8);
                    for (int i = 0; i < 8; i++)
                    {
                        _argsPool[i] = new ConnectionEventArgs(this);
                    }
                }
            }
        }

        for (int i = 0; i < 8; i++)
        {
            long bit = 1L << i;
            if ((Interlocked.Read(ref _argsPoolMask) & bit) == 0 &&
                (Interlocked.Or(ref _argsPoolMask, bit) & bit) == 0)
            {
                return _argsPool[i];
            }
        }

        return null;
    }

    internal bool ReturnEventArgsInternal(ConnectionEventArgs args)
    {
        ConnectionEventArgs[]? pool = _argsPool;
        if (pool == null)
        {
            return false;
        }

        for (int i = 0; i < 8; i++)
        {
            if (ReferenceEquals(pool[i], args))
            {
                long bit = 1L << i;
                pool[i].ResetForPool();
                _ = Interlocked.And(ref _argsPoolMask, ~bit);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Acquires a transition context from the connection's local pool.
    /// Used by AsyncCallback to execute packet handoffs without global pooling.
    /// </summary>
    internal PooledConnectEventContext? AcquireContext()
    {
        if (_contextPool == null)
        {
            lock (this)
            {
                if (_contextPool == null)
                {
                    _contextPool = System.Buffers.ArrayPool<PooledConnectEventContext>.Shared.Rent(8);
                    for (int i = 0; i < 8; i++)
                    {
                        _contextPool[i] = new PooledConnectEventContext { LocalOwner = this };
                    }
                }
            }
        }

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

    internal void ReturnContextInternal(PooledConnectEventContext context)
    {
        PooledConnectEventContext[]? pool = _contextPool;
        if (pool == null)
        {
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            if (ReferenceEquals(pool[i], context))
            {
                long bit = 1L << i;
                pool[i].ResetForPool();
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
            e.Dispose();
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
        if (e is null || sender is not Connection self)
        {
            e?.Dispose();
            return;
        }

        try
        {
            _ = Interlocked.Exchange(ref self._isDispatchingClose, 1);
            if (self._onCloseEvent != null)
            {
                Delegate[] handlers = self._onCloseEvent.GetInvocationList();
                foreach (EventHandler<IConnectEventArgs> handler in handlers)
                {
                    try
                    {
                        handler(self, e);
                    }
                    catch (Exception handlerEx) when (ExceptionClassifier.IsNonFatal(handlerEx))
                    {
                        if (self._logger != null && self._logger.IsEnabled(LogLevel.Error))
                        {
                            self._logger.LogError(handlerEx, $"[NW.{nameof(Connection)}:{nameof(OnCloseEventDispatchBridge)}] close-handler-error");
                        }
                    }
                }
            }
        }
        finally
        {
            _ = Interlocked.Exchange(ref self._isDispatchingClose, 0);
            e.Dispose();

            // If the socket signaled the close (via bridge) and Dispose() was never called
            // by the user, OR if it was called but skipped cleanup because it saw
            // the bridge was already signaled, we ensure cleanup happens here.
            if (Volatile.Read(ref self._disposeState) != 2)
            {
                self.PerformDestructiveCleanup();
            }
        }
    }

    #endregion Event Bridges
}
