// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
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
public sealed class WebSocketConnection : IConnection, IConnectionErrorTracked, TimingWheel.ITimeoutTrackedConnection
{
    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
    private static readonly ConnectionLimitOptions s_limitOptions = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
    private static readonly NetworkCallbackOptions s_callbackOptions = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();

    private readonly ILogger? _logger;
    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private int _errorCount;
    private int _closeSignaled;
    private int _disposeState; // 0=Active, 1=Closing, 2=Disposed
    private volatile bool _disposed;

    private long _bytesSent;
    private long _bytesReceived;
    private int _pendingProcessCallbacks;

    private IObjectMap<string, object>? _attributes;
    private IConnection.ITransport? _tcp;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

    // Per-connection local pool for packet arguments to mirror TCP ownership semantics.
    internal readonly LocalPool<ConnectionEventArgs> _argsPool;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketConnection"/> class.
    /// </summary>
    /// <param name="webSocket">The underlying WebSocket instance.</param>
    /// <param name="remoteEndPoint">The remote endpoint of the client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="webSocket"/> is null.</exception>
    public WebSocketConnection(WebSocket webSocket, EndPoint remoteEndPoint, ILogger? logger = null)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _logger = logger;

        this.ID = Snowflake.NewId(SnowflakeType.Session);
        this.NetworkEndpoint = SocketEndpoint.FromEndPoint(remoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 0));
        this.Secret = Bytes32.Zero;
        _argsPool = new LocalPool<ConnectionEventArgs>(s_pool);

        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"[NW.{nameof(WebSocketConnection)}] created remote={this.NetworkEndpoint} id={this.ID}");
        }
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public bool IsUdpCreated => false; // UDP is not supported over WebSocket

    /// <inheritdoc/>
    public ISnowflake ID { get; }

    /// <inheritdoc/>
    public IConnection.ITransport TCP => _tcp ??= new WebSocketTransport(this);

    /// <inheritdoc/>
    public IConnection.ITransport UDP => throw new NotSupportedException("UDP is not supported over WebSocket connections.");

    /// <inheritdoc/>
    public INetworkEndpoint NetworkEndpoint { get; }

    /// <inheritdoc/>
    public IObjectMap<string, object> Attributes => _attributes ??= ObjectMap<string, object>.Rent();

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
    internal ILogger? Logger => _logger;

    internal void AddBytesSent(long count) => Interlocked.Add(ref _bytesSent, count);
    internal void AddBytesReceived(long count) => Interlocked.Add(ref _bytesReceived, count);
    internal void UpdateLastPingTime() => this.LastPingTime = Clock.UnixMillisecondsNow();

    internal void TriggerPostProcessEvent()
    {
        if (_onPostProcessEvent != null)
        {
            ConnectionEventArgs args = this.AcquireEventArgs();
            args.Initialize(this);
            try
            {
                _onPostProcessEvent.Invoke(this, args);
            }
            finally
            {
                args.Dispose();
            }
        }
    }

    internal void TriggerProcessEvent(BufferLease lease)
    {
        int pending = Interlocked.Increment(ref _pendingProcessCallbacks);
        if (pending > s_callbackOptions.MaxPerConnectionPendingPackets)
        {
            _ = Interlocked.Decrement(ref _pendingProcessCallbacks);
            lease.Dispose();

            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"[NW.{nameof(WebSocketConnection)}] receive throttle triggered remote={this.NetworkEndpoint}");
            }
            return;
        }

        ConnectionEventArgs args = this.AcquireEventArgs();
        args.Initialize(lease, this);

        // Ensure dispatch is offloaded to avoid blocking the receive loop
        bool queued = ThreadPool.UnsafeQueueUserWorkItem(state =>
        {
            (WebSocketConnection? self, ConnectionEventArgs? evArgs) = ((WebSocketConnection, ConnectionEventArgs))state;
            try
            {
                self._onProcessEvent?.Invoke(self, evArgs);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (self._logger != null && self._logger.IsEnabled(LogLevel.Error))
                {
                    self._logger.LogError(ex, $"[NW.{nameof(WebSocketConnection)}] Process event error");
                }
            }
            finally
            {
                _ = Interlocked.Decrement(ref self._pendingProcessCallbacks);
                evArgs.Dispose(); // This also disposes the lease
            }
        }, (this, args), preferLocal: true);

        if (!queued)
        {
            _ = Interlocked.Decrement(ref _pendingProcessCallbacks);
            args.Dispose();
        }
    }

    #endregion Internal Helpers

    #region Methods

    /// <inheritdoc/>
    public void Disconnect(string? reason = null)
    {
        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug($"[NW.{nameof(WebSocketConnection)}] disconnect request id={this.ID} remote={this.NetworkEndpoint} reason={reason}");
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
            // Break timing wheel reference
            TimingWheel.TimeoutTask? task = ((TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask;
            if (task is not null)
            {
                task.Conn = null;
                ((TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask = null;
            }

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
                    if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, $"[NW.{nameof(WebSocketConnection)}] Close event error");
                    }
                }
                finally
                {
                    args.Dispose();
                }
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

            try { _argsPool.Destroy(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            try { _sendLock.Dispose(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
        }
    }

    #endregion Dispose Pattern

    #region Internal Pooling

    internal ConnectionEventArgs AcquireEventArgs()
    {
        ConnectionEventArgs? argLocal = _argsPool.Acquire(arg => arg.Initialize(this));
        if (argLocal != null)
        {
            return argLocal;
        }

        ConnectionEventArgs argGlobal = s_pool.Get<ConnectionEventArgs>();
        argGlobal.Initialize(this);

        return argGlobal;
    }

    internal void ReturnEventArgs(ConnectionEventArgs args) => _argsPool.Return(args);

    #endregion Internal Pooling
}
