// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
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
using Nalix.Network.Internal.Time;
using Nalix.Network.Internal.Transport;
using Nalix.Network.Options;

namespace Nalix.Network.Connections;

/// <summary>
/// Represents a network connection that manages WebSocket communication, stream
/// transformation, and event handling.
/// </summary>
public sealed class WebSocketConnection :
    IConnection,
    IConnectionErrorTracked,
    IConnectionTrafficMetrics,
    IPooledConnectContextPool,
    TimingWheel.ITimeoutTrackedConnection
{
    #region Fields

    private static readonly ObjectPoolManager s_pool;
    private static readonly TimingWheel s_timingWheel;
    private static readonly TimingWheelOptions s_timingWheelOptions;
    private static readonly ConnectionGuardOptions s_limitOptions;
    private static readonly NetworkCallbackOptions s_callbackOptions;

    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private int _errorCount;
    private int _closeSignaled;
    private int _disposeState; // 0=Active, 1=Closing, 2=Disposed
    private volatile bool _disposed;

    private long _bytesSent;
    private long _bytesReceived;
    private long _packetsDropped;
    private int _pendingProcessCallbacks;

    private WebSocketTransport? _tcp;
    private IObjectMap<AttributeKey, object>? _attributes;
    private ConcurrentDictionary<ushort, object>? _rateLimitCache;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    internal LocalPool<ConnectionEventArgs> _argsPool;
    internal LocalPool<PooledConnectEventContext> _contextPool;

    #endregion Fields

    #region Constructor

    static WebSocketConnection()
    {
        s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
        s_timingWheel = InstanceManager.Instance.GetOrCreateInstance<TimingWheel>();

        s_limitOptions = ConfigurationManager.Instance.Get<ConnectionGuardOptions>();
        s_timingWheelOptions = ConfigurationManager.Instance.Get<TimingWheelOptions>();
        s_callbackOptions = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketConnection"/> class.
    /// </summary>
    /// <param name="webSocket">The underlying WebSocket instance.</param>
    /// <param name="remoteEndPoint">The remote endpoint of the client.</param>
    /// <param name="packetClassifier">The opcode extractor for classifying incoming packets.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="webSocket"/> is null.</exception>
    public WebSocketConnection(WebSocket webSocket, IOpCodeExtractor packetClassifier, EndPoint remoteEndPoint)
    {
        ArgumentNullException.ThrowIfNull(webSocket);
        ArgumentNullException.ThrowIfNull(remoteEndPoint);
        ArgumentNullException.ThrowIfNull(packetClassifier);

        _webSocket = webSocket;

        this.Secret = Bytes32.Zero;
        this.PacketClassifier = packetClassifier;
        this.IdleTimeoutMs = s_timingWheelOptions.IdleTimeoutMs;
        this.ID = Snowflake.NewId(SnowflakeType.Session).ToUInt64();
        this.NetworkEndpoint = SocketEndpoint.FromEndPoint(remoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 0));

        _argsPool = new LocalPool<ConnectionEventArgs>(s_pool);
        _contextPool = new LocalPool<PooledConnectEventContext>(s_pool);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.WebSocketConnection:UnknownMethod", $"created remote-endpoint={this.NetworkEndpoint} connection-id={this.ID:X16}"));
        }
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public bool IsUdpCreated => false; // UDP is not supported over WebSocket

    /// <inheritdoc/>
    public bool ExcludeFromIdleTimeout { get; set; }

    /// <inheritdoc/>
    public ulong ID { get; }

    /// <inheritdoc/>
    public IOpCodeExtractor PacketClassifier { get; }

    /// <inheritdoc/>
    public IConnection.ITransport TCP => _tcp ??= new WebSocketTransport(this);

    /// <inheritdoc/>
    public IConnection.ITransport? UDP => null;

    /// <inheritdoc/>
    public INetworkEndpoint NetworkEndpoint { get; }

    /// <inheritdoc />
    public IObjectMap<AttributeKey, object> Attributes => _attributes ??= ObjectMap<AttributeKey, object>.Rent();

    /// <inheritdoc />
    public ConcurrentDictionary<ushort, object> RateLimitCache => _rateLimitCache ??= new();

    /// <inheritdoc/>
    public int ErrorCount => _errorCount;

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public long UpTime { get => (long)Clock.UnixTime().TotalMilliseconds - field; } = (long)Clock.UnixTime().TotalMilliseconds;

    /// <inheritdoc/>
    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <inheritdoc/>
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <inheritdoc/>
    public long PacketsDropped => Interlocked.Read(ref _packetsDropped);

    /// <inheritdoc/>
    public void IncrementBytesSent(int bytes) => Interlocked.Add(ref _bytesSent, bytes);

    /// <inheritdoc/>
    public void IncrementBytesReceived(int bytes) => Interlocked.Add(ref _bytesReceived, bytes);

    /// <inheritdoc/>
    public void IncrementPacketsDropped() => Interlocked.Increment(ref _packetsDropped);

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// </summary>
    public long LastPingTime
    {
        get => Interlocked.Read(ref field);
        set => Interlocked.Exchange(ref field, value);
    } = Clock.UnixMillisecondsNow();

    /// <inheritdoc/>
    public PermissionLevel Level { get; set; } = PermissionLevel.NONE;

    /// <inheritdoc/>
    public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;

    /// <inheritdoc/>
    public Bytes32 Secret { get; set; }

    /// <inheritdoc />
    public int IdleTimeoutMs { get; set; }

    /// <inheritdoc/>
    public int TimeoutVersion { get; set; }

    /// <inheritdoc/>
    public bool IsRegisteredInWheel { get; set; }

    /// <summary>
    /// Tracks the current timeout task in the TimingWheel.
    /// Used for manual reference breaking during Dispose to allow instant GC.
    /// </summary>
    TimingWheel.TimeoutTask? TimingWheel.ITimeoutTrackedConnection.TimeoutTask { get; set; }

    #endregion Properties

    #region Events

    /// <inheritdoc/>
    public event EventHandler<IConnectEventArgs> OnCloseEvent
    {
        add => _onCloseEvent += value;
        remove => _onCloseEvent -= value;
    }

    /// <inheritdoc/>
    public event EventHandler<IConnectEventArgs> OnProcessEvent
    {
        add => _onProcessEvent += value;
        remove => _onProcessEvent -= value;
    }

    /// <inheritdoc/>
    public event EventHandler<IConnectEventArgs> OnPostProcessEvent
    {
        add => _onPostProcessEvent += value;
        remove => _onPostProcessEvent -= value;
    }

    #endregion Events

    #region Internal Helpers


    internal WebSocket WebSocket => _webSocket;
    internal SemaphoreSlim SendLock => _sendLock;

    internal void AddBytesSent(long count) => Interlocked.Add(ref _bytesSent, count);
    internal void AddBytesReceived(long count) => Interlocked.Add(ref _bytesReceived, count);
    internal void UpdateLastPingTime() => this.LastPingTime = Clock.UnixMillisecondsNow();

    internal void TriggerPostProcessEvent()
    {
        if (_onPostProcessEvent is null)
        {
            return;
        }

        ConnectionEventArgs args = this.AcquireEventArgs();
        args.Initialize(this);

        if (!Internal.Transport.AsyncCallback.Invoke(OnPostProcessEventBridge, this, args, CallbackLane.Post))
        {
            args.Dispose();
        }
    }

    internal void TriggerProcessEvent(BufferLease lease)
    {
        int pending = Interlocked.Increment(ref _pendingProcessCallbacks);
        if (pending > s_callbackOptions.MaxPerConnectionPendingPackets)
        {
            _ = Interlocked.Decrement(ref _pendingProcessCallbacks);
            lease.Dispose();
            this.IncrementPacketsDropped();

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.WebSocketConnection:TriggerProcessEvent", $"receive throttle triggered remote-endpoint={this.NetworkEndpoint}"));
            }
            return;
        }

        ConnectionEventArgs args = this.AcquireEventArgs();
        args.Initialize(lease, this);

        if (!Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args, CallbackLane.Process, releasePendingPacketOnCompletion: true))
        {
            ((IPooledConnectContextPool)this).ReleasePendingPacket();
            _ = args.ExchangeLease(null);
            args.Dispose();
            lease.Dispose();
        }
    }

    private static void OnProcessEventBridge(object? sender, IConnectEventArgs e)
    {
        if (e is null)
        {
            return;
        }

        if (sender is not WebSocketConnection self)
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

    private static void OnPostProcessEventBridge(object? sender, IConnectEventArgs e)
    {
        if (e is null)
        {
            return;
        }

        if (sender is not WebSocketConnection self)
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

    #endregion Internal Helpers

    #region Methods

    /// <inheritdoc/>
    public void Disconnect(string? reason = null)
    {
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.WebSocketConnection:Disconnect", $"disconnect request connection-id={this.ID:X16} remote-endpoint={this.NetworkEndpoint} reason={reason}"));
        }
        this.Dispose();
    }

    /// <inheritdoc/>
    public void IncrementErrorCount()
    {
        int count = Interlocked.Increment(ref _errorCount);

        if (s_limitOptions.MaxErrorThreshold > 0 && count >= s_limitOptions.MaxErrorThreshold)
        {
            this.Disconnect("Exceeded maximum error threshold.");
        }
    }

    /// <inheritdoc />
    public void UpdateIdleTimeout(int newTimeoutMs)
    {
        if (newTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newTimeoutMs), "Idle timeout must be a positive integer.");
        }

        if (this.IdleTimeoutMs == newTimeoutMs)
        {
            return; // No change needed
        }

        this.IdleTimeoutMs = newTimeoutMs;

        s_timingWheel.Unregister(this);
        s_timingWheel.Register(this);
    }

    #endregion Methods

    #region Dispose Pattern

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (Interlocked.Exchange(ref _closeSignaled, 1) == 0 && _onCloseEvent != null)
            {
                ConnectionEventArgs args = this.AcquireEventArgs();
                args.Initialize(this);
                try
                {
                    _onCloseEvent.Invoke(this, args);
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketConnection:Dispose", "Close event error", ex));
                    }
                }
                finally
                {
                    args.Dispose();
                }
            }

            // Break timing wheel reference AFTER close handlers have run
            // so TimingWheel.Unregister can retrieve and remove the TimeoutTask from the bucket.
            TimingWheel.TimeoutTask? task = ((TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask;
            if (task is not null)
            {
                task.Conn = null;
                ((TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask = null;
            }
        }
        finally
        {
            Volatile.Write(ref _disposeState, 2);
            _disposed = true;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    _webSocket.Abort();
                }
                _webSocket.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            _attributes?.Return();
            _attributes = null;

            try { Interlocked.Exchange(ref _tcp, null)?.Dispose(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            try { _argsPool.Destroy(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            try { _contextPool.Destroy(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            try { _sendLock.Dispose(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
        }
    }

    #endregion Dispose Pattern

    #region Internal Pooling

    internal ConnectionEventArgs AcquireEventArgs()
    {
        ConnectionEventArgs? argLocal = _argsPool.Acquire(this, static (arg, self) => arg.Initialize(self));
        if (argLocal != null)
        {
            return argLocal;
        }

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(this);

        return args;
    }

    internal void ReturnEventArgs(ConnectionEventArgs args) => _argsPool.Return(args);

    PooledConnectEventContext IPooledConnectContextPool.AcquireContext()
    {
        PooledConnectEventContext? ctxLocal = _contextPool.Acquire(this, static (ctx, self) => ctx.LocalOwner = self);
        if (ctxLocal != null)
        {
            ctxLocal.LocalOwner = this;
            return ctxLocal;
        }

        PooledConnectEventContext ctxGlobal = s_pool.Get<PooledConnectEventContext>();
        ctxGlobal.LocalOwner = this;

        return ctxGlobal;
    }

    void IPooledConnectContextPool.ReleasePendingPacket() => Interlocked.Decrement(ref _pendingProcessCallbacks);

    void IPooledConnectContextPool.ReturnContext(PooledConnectEventContext context) => _contextPool.Return(context);

    #endregion Internal Pooling
}
