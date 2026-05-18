// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Environment.Memory;
using Nalix.Environment.Time;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Security;
using Nalix.Network.Internal.Time;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Connections;

/// <summary>
/// Represents a network connection that manages WebSocket communication, stream
/// transformation, and event handling.
/// </summary>
public sealed class WebSocketConnection : IConnection, IConnectionErrorTracked, TimingWheel.ITimeoutTrackedConnection
{
    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private readonly ILogger? _logger;
    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private int _errorCount;
    private int _closeSignaled;
    private int _disposeState; // 0=Active, 1=Closing, 2=Disposed
    private volatile bool _disposed;

    private long _bytesSent;
    private long _bytesReceived;

    private IObjectMap<string, object>? _attributes;
    private IConnection.ITransport? _tcp;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

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
    public void IncrementErrorCount() => Interlocked.Increment(ref _errorCount);

    #endregion Methods

    #region Receive Loop

    /// <summary>
    /// Starts the asynchronous receive loop for the WebSocket connection.
    /// </summary>
    public async Task StartReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        // Allocate a buffer for reading frames (default 64KB)
        const int receiveBufferSize = 65536;
        byte[] buffer = BufferLease.ByteArrayPool.Rent(receiveBufferSize);

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open && !_disposed)
            {
                WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                _ = Interlocked.Add(ref _bytesReceived, result.Count);
                this.LastPingTime = Clock.UnixMillisecondsNow();

                if (result.EndOfMessage)
                {
                    // Fast path: the entire message fit in our buffer
                    this.DispatchPayload(buffer, 0, result.Count);
                }
                else
                {
                    // Slow path: the message is larger than the buffer, we need to assemble it
                    await this.HandleLargeMessageAsync(buffer, result.Count, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException)
        {
            // Disconnected
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(WebSocketConnection)}] Receive loop error");
            }
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);
            this.Disconnect("Receive loop exited");
        }
    }

    private async Task HandleLargeMessageAsync(byte[] initialBuffer, int initialBytes, CancellationToken cancellationToken)
    {
        using MemoryStream ms = new();
        await ms.WriteAsync(initialBuffer.AsMemory(0, initialBytes), cancellationToken).ConfigureAwait(false);

        byte[] buffer = BufferLease.ByteArrayPool.Rent(65536);
        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                await ms.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
                _ = Interlocked.Add(ref _bytesReceived, result.Count);

            } while (!result.EndOfMessage);

            // Dispatch the fully assembled message
            if (ms.TryGetBuffer(out ArraySegment<byte> segment))
            {
                this.DispatchPayload(segment.Array!, segment.Offset, segment.Count);
            }
            else
            {
                byte[] array = ms.ToArray();
                this.DispatchPayload(array, 0, array.Length);
            }
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);
        }
    }

    private void DispatchPayload(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return;
        }

        // Rent a lease and copy data so the receive loop can continue immediately
        BufferLease lease = BufferLease.CopyFrom(new ReadOnlySpan<byte>(buffer, offset, count));
        lease.IsReliable = true;

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(lease, this);

        // Ensure dispatch is offloaded to avoid blocking the receive loop
        _ = ThreadPool.UnsafeQueueUserWorkItem(state =>
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
                evArgs.Dispose(); // This also disposes the lease
            }
        }, (this, args), preferLocal: true);
    }

    #endregion Receive Loop

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
            Nalix.Network.Internal.Time.TimingWheel.TimeoutTask? task = ((Nalix.Network.Internal.Time.TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask;
            if (task is not null)
            {
                task.Conn = null;
                ((Nalix.Network.Internal.Time.TimingWheel.ITimeoutTrackedConnection)this).TimeoutTask = null;
            }

            // Signal close event first
            if (Interlocked.Exchange(ref _closeSignaled, 1) == 0 && _onCloseEvent != null)
            {
                ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
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

            try { _sendLock.Dispose(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
        }
    }

    #endregion Dispose Pattern

    #region Adapter

    /// <summary>
    /// Adapter class that implements <see cref="IConnection.ITransport"/> for WebSocket.
    /// </summary>
    private sealed class WebSocketTransport : IConnection.ITransport
    {
        private TransportSequencer _sequencer;
        private readonly WebSocketConnection _owner;

        public WebSocketTransport(WebSocketConnection owner)
        {
            _owner = owner;
            _sequencer = new TransportSequencer();
        }

        public ISequenceCounter SendSequence => _sequencer.SendSequence;

        public ISequenceCounter ReceiveSequence => _sequencer.ReceiveSequence;

        public void Send(IPacket packet) => this.SendAsync(packet).AsTask().GetAwaiter().GetResult();

        public void Send(ReadOnlySpan<byte> message) => this.SendAsync(message.ToArray()).AsTask().GetAwaiter().GetResult();

        public async ValueTask SendAsync(IPacket packet, CancellationToken cancellationToken = default)
        {
            byte[] bytes = packet.Serialize();
            await this.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
        {
            if (_owner._disposed || _owner._webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is closed.");
            }

            // WebSockets handle framing natively, so we just send the message as binary.
            // A SemaphoreSlim is used because WebSocket.SendAsync doesn't support concurrent calls.
            await _owner._sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _owner._webSocket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = _owner._sendLock.Release();
            }

            _ = Interlocked.Add(ref _owner._bytesSent, message.Length);

            // Invoke post process
            if (_owner._onPostProcessEvent != null)
            {
                ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
                args.Initialize(_owner);
                try
                {
                    _owner._onPostProcessEvent.Invoke(_owner, args);
                }
                finally
                {
                    args.Dispose();
                }
            }
        }

        public void BeginReceive(CancellationToken cancellationToken = default) => _ = _owner.StartReceiveLoopAsync(cancellationToken);
    }

    #endregion Adapter
}
