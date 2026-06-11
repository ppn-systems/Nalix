// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Environment.Configuration;
using Nalix.Environment.Memory;
using Nalix.Environment.Time;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Internal.Security;
using Nalix.Network.Internal.Time;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Options;

namespace Nalix.Network.Connections;

/// <summary>
/// Represents a network connection that manages socket communication, stream
/// transformation, and event handling.
/// This is the high-level owner for the socket transport and the per-connection
/// event pipeline.
/// </summary>
public sealed partial class Connection :
    IConnection,
    IConnectionErrorTracked,
    IConnectionTrafficMetrics,
    IPooledConnectContextPool,
    TimingWheel.ITimeoutTrackedConnection
{
    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
    private static readonly ConnectionGuardOptions s_options = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();
    private static readonly DatagramGuardOptions s_datagramOptions = ConfigurationManager.Instance.Get<DatagramGuardOptions>();
    private static readonly NetworkCallbackOptions s_callbackOptions = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();

    private readonly Lock _lock;
    private readonly SocketConnection _socket;
    private readonly SocketTcpTransport _tcpTransport;

    private long _bytesSent;
    private long _bytesReceived;
    private long _packetsDropped;

    private int _errorCount;
    private int _disposeState; // 0=Active, 1=Closing(Event running), 2=Disposed
    private int _closeSignaled;
    private int _isDispatchingClose; // 0=no, 1=yes
    private int _pendingProcessCallbacks;

    private SlidingWindow? _udpReplayWindow;
    private IObjectMap<string, object>? _attributes;
    private ConcurrentDictionary<ushort, object>? _rateLimitCache;

    private volatile bool _disposed;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    // Per-connection local pool for packet arguments to avoid global pool contention.
    // Size 8 matches the default MaxPerConnectionPendingPackets.
    internal LocalPool<ConnectionEventArgs> _argsPool;

    internal LocalPool<PooledConnectEventContext> _contextPool;

    #endregion Fields

    #region Constructor

    /// <summary>Initializes a new instance of the <see cref="Connection"/> class.</summary>
    /// <param name="socket">The connected socket used for the connection.</param>
    /// <param name="packetClassifier">The opcode extractor for classifying incoming packets.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="socket"/> is null.</exception>
    public Connection(Socket socket, IOpCodeExtractor packetClassifier)
        : this(socket, packetClassifier, socket?.RemoteEndPoint ?? throw new InternalErrorException("Socket does not expose a remote endpoint."))
    {
    }

    /// <summary>
    /// Initializes a Connection with an overridden real endpoint (Proxy Protocol).
    /// Use this overload when the TCP peer is a proxy that injects a PROXY header.
    /// </summary>
    public Connection(Socket socket, IOpCodeExtractor packetClassifier, System.Net.EndPoint realEndPoint)
    {
        ArgumentNullException.ThrowIfNull(realEndPoint);
        ArgumentNullException.ThrowIfNull(packetClassifier);

        _disposed = false;
        _lock = new Lock();

        _argsPool = new LocalPool<ConnectionEventArgs>(s_pool);
        _contextPool = new LocalPool<PooledConnectEventContext>(s_pool);

        this.Secret = Bytes32.Zero;
        this.PacketClassifier = packetClassifier;
        // Snapshot the remote endpoint up front so the connection can be logged
        // and tracked even before protocol-level events begin.
        this.ID = Snowflake.NewId(SnowflakeType.Session).ToUInt64();

        // Use realEndPoint (from PROXY header) instead of socket.RemoteEndPoint (LB IP).
        this.NetworkEndpoint = SocketEndpoint.FromEndPoint(realEndPoint);

        // Initialize the socket connection.
        _socket = new SocketConnection(socket, this);
        _tcpTransport = new SocketTcpTransport(this, _socket);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.Connection:UnknownMethod", $"created remote=remote-endpoint={this.NetworkEndpoint} id=connection-id={this.ID:X16}"));
        }
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public bool IsUdpCreated => this.UdpTransport is not null;

    /// <inheritdoc/>
    public bool ExcludeFromIdleTimeout { get; set; } = true;

    /// <inheritdoc />
    public ulong ID { get; }

    /// <inheritdoc/>
    public IConnection.ITransport TCP => _tcpTransport;

    /// <inheritdoc/>
    public IConnection.ITransport? UDP
    {
        get
        {
            if (this.UdpTransport is not { } udp)
            {
                return null;
            }
            return udp;
        }
    }
    /// <inheritdoc/>
    public IOpCodeExtractor PacketClassifier { get; }

    /// <inheritdoc />
    public INetworkEndpoint NetworkEndpoint { get; }

    /// <inheritdoc />
    public IObjectMap<string, object> Attributes => _attributes ??= ObjectMap<string, object>.Rent();

    /// <inheritdoc />
    public ConcurrentDictionary<ushort, object> RateLimitCache => _rateLimitCache ??= new();

    /// <inheritdoc />
    public int ErrorCount => _errorCount;

    /// <inheritdoc />
    public long UpTime { get => (long)Clock.UnixTime().TotalMilliseconds - field; } = (long)Clock.UnixTime().TotalMilliseconds;

    /// <inheritdoc />
    public long LastPingTime => _socket.LastPingTime;

    /// <summary>
    /// Returns the number of packets currently pending in the async callback pipeline.
    /// </summary>
    public int PendingPackets => Volatile.Read(ref _pendingProcessCallbacks);

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

    /// <summary>
    /// Tracks the current timeout task in the TimingWheel.
    /// Used for manual reference breaking during Dispose to allow instant GC.
    /// </summary>
    TimingWheel.TimeoutTask? TimingWheel.ITimeoutTrackedConnection.TimeoutTask { get; set; }

    /// <summary>
    /// Gets the total number of bytes sent over the life of the connection.
    /// </summary>
    /// <remarks>
    /// This value combines data from the underlying <see cref="SocketConnection.BytesSent"/> (TCP)
    /// and the <see cref="SocketUdpTransport.BytesSent"/> (UDP) if available.
    /// It represents raw wire data, including protocol headers.
    /// </remarks>
    public long BytesSent => _socket.BytesSent + (this.UdpTransport?.BytesSent ?? 0) + Volatile.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total number of bytes received over the life of the connection.
    /// </summary>
    /// <remarks>
    /// This value combines data from the underlying <see cref="SocketConnection.BytesReceived"/> (TCP)
    /// and the <see cref="SocketUdpTransport.BytesReceived"/> (UDP) if available.
    /// It represents raw wire data before any frame processing or decompression.
    /// </remarks>
    public long BytesReceived => _socket.BytesReceived + (this.UdpTransport?.BytesReceived ?? 0) + Volatile.Read(ref _bytesReceived);

    /// <inheritdoc />
    public long PacketsDropped => Volatile.Read(ref _packetsDropped);

    /// <inheritdoc />
    public void IncrementPacketsDropped() => Interlocked.Increment(ref _packetsDropped);

    #endregion Properties

    #region Internal

    internal SocketUdpTransport? UdpTransport { get; private set; }

    internal SlidingWindow UdpReplayWindow => _udpReplayWindow ??= new(s_datagramOptions.UdpReplayWindowSize);

#if DEBUG
    /// <summary>
    /// Injects a packet directly into the process pipeline for testing.
    /// This bypasses the socket receive loop but still triggers AsyncCallback
    /// and respects the per-connection throttle.
    /// </summary>
    internal void InjectIncoming(Environment.Memory.BufferLease lease)
    {
        ConnectionEventArgs? args = this.AcquireEventArgs();

        if (args == null)
        {
            return;
        }

        _ = Interlocked.Increment(ref _pendingProcessCallbacks);
        args.Initialize(lease, this);

        if (!Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args, CallbackLane.Process, releasePendingPacketOnCompletion: true))
        {
            ((IPooledConnectContextPool)this).ReleasePendingPacket();
            _ = args.ExchangeLease(null);
            args.Dispose();
            lease.Dispose();
        }
    }
#endif

    internal void InjectPreReadBytes(ReadOnlySpan<byte> preReadData) => _socket.InjectPreReadBytes(preReadData);

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
    public void IncrementErrorCount()
    {
        int count = Interlocked.Increment(ref _errorCount);

        // SEC-54: Disconnect persistent noisy/malformed connections
        if (s_options.MaxErrorThreshold > 0 && count >= s_options.MaxErrorThreshold)
        {
            this.Disconnect("Exceeded maximum error threshold.");
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementBytesSent(int bytes) => Interlocked.Add(ref _bytesSent, bytes);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementBytesReceived(int bytes) => Interlocked.Add(ref _bytesReceived, bytes);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Disconnect(string? reason = null)
    {
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.Connection:Disconnect", $"disconnect request id={this.ID} remote={this.NetworkEndpoint} reason={reason}"));
        }

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
                        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
                        args.Initialize(this);

                        try
                        {
                            Delegate[] handlers = _onCloseEvent.GetInvocationList();
                            for (int i = 0; i < handlers.Length; i++)
                            {
                                EventHandler<IConnectEventArgs> handler = (EventHandler<IConnectEventArgs>)handlers[i];
                                try
                                {
                                    handler(this, args);
                                }
                                catch (Exception handlerEx) when (ExceptionClassifier.IsNonFatal(handlerEx))
                                {
                                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                                    {
                                        // handlerEx, "[NW.Connection:this.Dispose] close-handler-error");
                                    }
                                }
                            }
                        }
                        finally
                        {
                            args.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.Connection:Dispose", "close-event-error", ex));
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

    [MethodImpl(MethodImplOptions.NoInlining)]
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
            TimingWheel.TimeoutTask? task = ((TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask;
            if (task is not null)
            {
                task.Conn = null;
                ((TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask = null;
            }

            try { _socket.Dispose(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "socket"); }

            try
            {
                if (this.UdpTransport != null)
                {
                    s_pool.Return(this.UdpTransport);
                    this.UdpTransport = null;
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "udptransport"); }

            try
            {
                // Return local pooled objects to global pool to prevent "leak" when connection is destroyed.
                // Without this, every connection "steals" 8 args and 8 contexts from the global pool forever.
                _argsPool.Destroy();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { LOG_ERROR(ex, "argspool"); }

            try
            {
                _contextPool.Destroy();
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

        static void LOG_ERROR(Exception ex, string component)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.Connection:Internal", $"component={component}-dispose-error", ex));
            }
        }
    }

    #endregion Dispose Pattern

    #region Internal Pooling

    /// <summary>
    /// Acquires an EventArgs from the connection's local pool for packet processing.
    /// Returns null if the local pool is exhausted (throttle reached).
    /// </summary>
    internal ConnectionEventArgs AcquireEventArgs()
    {
        ConnectionEventArgs? arg_local = _argsPool.Acquire(this, static (arg, self) => arg.Initialize(self));
        if (arg_local != null)
        {
            return arg_local;
        }

        ConnectionEventArgs? arg_global = s_pool.Get<ConnectionEventArgs>();
        arg_global.Initialize(this);

        return arg_global;
    }

    internal void ReturnEventArgs(ConnectionEventArgs args) => _argsPool.Return(args);

    /// <summary>
    /// Acquires a transition context from the connection's local pool.
    /// Used by AsyncCallback to execute packet handoffs without global pooling.
    /// </summary>
    PooledConnectEventContext IPooledConnectContextPool.AcquireContext()
    {
        PooledConnectEventContext? arg_local = _contextPool.Acquire(this, static (ctx, self) => ctx.LocalOwner = self);
        if (arg_local != null)
        {
            return arg_local;
        }

        PooledConnectEventContext? arg_global = s_pool.Get<PooledConnectEventContext>();
        arg_global.LocalOwner = this;

        return arg_global;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IPooledConnectContextPool.ReleasePendingPacket() => Interlocked.Decrement(ref _pendingProcessCallbacks);

    void IPooledConnectContextPool.ReturnContext(PooledConnectEventContext context) => _contextPool.Return(context);

    #endregion Internal Pooling

    #region SocketConnection Callbacks

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal bool OnFrameReceived(BufferLease lease, bool isReliable)
    {
        _ = isReliable;
        int pending = Interlocked.Increment(ref _pendingProcessCallbacks);
        if (pending > s_callbackOptions.MaxPerConnectionPendingPackets)
        {
            _ = Interlocked.Decrement(ref _pendingProcessCallbacks);
            return false;
        }

        ConnectionEventArgs args = this.AcquireEventArgs();
        args.Initialize(lease, this);

        if (!Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args, CallbackLane.Process, releasePendingPacketOnCompletion: true))
        {
            _ = Interlocked.Decrement(ref _pendingProcessCallbacks);
            _ = args.ExchangeLease(null);
            args.Dispose();
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnFrameSent()
    {
        ConnectionEventArgs args = this.AcquireEventArgs();
        args.Initialize(this);

        if (!Internal.Transport.AsyncCallback.Invoke(OnPostProcessEventBridge, this, args, CallbackLane.Post))
        {
            args.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnTransportClosed()
    {
        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(this);

        if (!Internal.Transport.AsyncCallback.InvokeHighPriority(this.OnCloseEventBridge, this, args))
        {
            args.Dispose();
        }
    }

    #endregion SocketConnection Callbacks

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

        SAFE_PROCESS_EVENT_BRIDGE(self, e);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SAFE_PROCESS_EVENT_BRIDGE(Connection self, IConnectEventArgs e)
    {
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

        SAFE_POST_PROCESS_EVENT_BRIDGE(self, e);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SAFE_POST_PROCESS_EVENT_BRIDGE(Connection self, IConnectEventArgs e)
    {
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

        SAFE_CLOSE_EVENT_DISPATCH_BRIDGE(self, e);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SAFE_CLOSE_EVENT_DISPATCH_BRIDGE(Connection self, IConnectEventArgs e)
    {
        try
        {
            _ = Interlocked.Exchange(ref self._isDispatchingClose, 1);
            if (self._onCloseEvent != null)
            {
                Delegate[] handlers = self._onCloseEvent.GetInvocationList();
                for (int i = 0; i < handlers.Length; i++)
                {
                    EventHandler<IConnectEventArgs> handler = (EventHandler<IConnectEventArgs>)handlers[i];
                    try
                    {
                        handler(self, e);
                    }
                    catch (Exception handlerEx) when (ExceptionClassifier.IsNonFatal(handlerEx))
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                        {
                            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.Connection:Internal", "close-handler-error", handlerEx));
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
