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
using Nalix.Common.Security;
using Nalix.Framework.Configuration;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Security.Primitives;
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

    private volatile bool _disposed;

    private EventHandler<IConnectEventArgs>? _onCloseEvent;
    private EventHandler<IConnectEventArgs>? _onProcessEvent;
    private EventHandler<IConnectEventArgs>? _onPostProcessEvent;

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

        this.Secret = [];
        // Snapshot the remote endpoint up front so the connection can be logged
        // and tracked even before protocol-level events begin.
        this.ID = Snowflake.NewId(SnowflakeType.Session);
        this.NetworkEndpoint = SocketEndpoint.FromEndPoint(socket?.RemoteEndPoint ?? throw new InternalErrorException("Socket does not expose a remote endpoint."));

        _args = new ConnectionEventArgs(this);
        this.Socket = new SocketConnection(socket, logger);

        // Wire the socket-level events into the connection-level callback pipeline.
        this.Socket.SetCallback(this, _args, this.OnCloseEventBridge, OnPostProcessEventBridge, OnProcessEventBridge);

        this.TCP = new SocketTcpTransport(this);
        this.Attributes = ObjectMap<string, object>.Rent();

        _logger?.Trace($"[NW.{nameof(Connection)}] created remote={this.NetworkEndpoint} id={this.ID}");
    }

    #endregion Constructor

    #region Properties

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
    public byte[] Secret
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            byte[] next = value ?? [];
            byte[]? previous = field;

            if (!ReferenceEquals(previous, next) && previous is { Length: > 0 })
            {
                MemorySecurity.ZeroMemory(previous);
            }

            field = next;
        }
    }

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

    internal bool IsDisposed => _disposed;

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

        _ = Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args, releasePendingPacketOnCompletion: true);
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
            if (this.Secret.Length > 0)
            {
                MemorySecurity.ZeroMemory(this.Secret);
                this.Secret = [];
            }

            // Return pooled metadata first so the connection does not keep
            // borrowed state alive after disposal begins.
            this.Attributes.Return();

            this.Disconnect();

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

    #region Event Bridges

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void OnCloseEventBridge(object? sender, IConnectEventArgs e)
    {
        if (Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        // Close events bypass backpressure because cleanup must never be delayed.
        _ = Internal.Transport.AsyncCallback.InvokeHighPriority(_onCloseEvent, e.Connection, e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void OnProcessEventBridge(object? sender, IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        self._onProcessEvent?.Invoke(self, e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void OnPostProcessEventBridge(object? sender, IConnectEventArgs e)
    {
        if (sender is not Connection self)
        {
            return;
        }

        self._onPostProcessEvent?.Invoke(self, e);
    }

    #endregion Event Bridges
}
