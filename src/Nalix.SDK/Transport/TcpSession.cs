// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a TCP transport session built on <see cref="FRAME_READER"/> and <see cref="FRAME_SENDER"/>.
/// </summary>
public partial class TcpSession : TransportSession, IWithLogging<TcpSession>
{
    #region Fields

    private ILogger? _logger;

    // Low-level components for reading and sending frames
    private readonly FRAME_SENDER _sender;
    private readonly FRAME_READER _reader;

    private Socket? _socket;
    private CancellationTokenSource? _loopCts;
    private int _disposed;

    #endregion Fields

    /// <summary>Gets the fixed framing header size in bytes.</summary>
    public const int HeaderSize = 2;

    /// <inheritdoc/>
    public override TransportOptions Options { get; }

    /// <inheritdoc/>
    public override IPacketRegistry Catalog { get; }

    /// <inheritdoc/>
    public override bool IsConnected => _socket?.Connected == true && Volatile.Read(ref _disposed) == 0;

    #region Events

    /// <inheritdoc/>
    public override event EventHandler? OnConnected;

    /// <inheritdoc/>
    public override event EventHandler<Exception>? OnDisconnected;

    /// <inheritdoc/>
    public override event EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public override event EventHandler<Exception>? OnError;

    /// <summary>Occurs when a complete frame is received and decoded asynchronously.</summary>
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageAsync;

    #endregion Events

    #region Constructor

    /// <summary>Initializes a new instance of the <see cref="TcpSession"/> class.</summary>
    /// <param name="options">The transport options for this session.</param>
    /// <param name="catalog">The packet registry used to resolve packet metadata.</param>
    /// <param name="logger">The optional logger used for transport diagnostics.</param>
    public TcpSession(TransportOptions options, IPacketRegistry catalog, ILogger? logger = null)
    {
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
        this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logger = logger;

        // Initialize frame helpers with a factory to get the latest socket instance
        _sender = new FRAME_SENDER(() => _socket!, options, this.HandleError);
        _reader = new FRAME_READER(() => _socket!, options, this.HandleReceiveMessage, this.HandleError);
    }

    /// <inheritdoc/>
    public TcpSession WithLogging(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    #endregion Constructor

    /// <inheritdoc/>
    public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(TcpSession));

        string effectiveHost = string.IsNullOrWhiteSpace(host) ? this.Options.Address : host;
        ushort effectivePort = port ?? this.Options.Port;

        // Ensure single connection at a time
        if (this.IsConnected)
        {
            await this.DisconnectAsync().ConfigureAwait(false);
        }

        try
        {
            // Initialize socket with NoDelay to reduce latency
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await _socket.ConnectAsync(effectiveHost, effectivePort, ct).ConfigureAwait(false);
            Log.Connected(_logger, effectiveHost, effectivePort);

            this.OnConnected?.Invoke(this, EventArgs.Empty);

            // Start background worker for reading frames
            _loopCts = new CancellationTokenSource();
            _ = Task.Run(() => _reader.ReceiveLoopAsync(_loopCts.Token), CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.ConnectFailed(_logger, effectiveHost, effectivePort, ex);
            this.OnError?.Invoke(this, ex);
            throw new NetworkException($"Connection failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public override Task DisconnectAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return Task.CompletedTask;
        }

        // Signal background loops to stop
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;

        if (_socket != null)
        {
            try { if (_socket.Connected) { _socket.Shutdown(SocketShutdown.Both); } } catch (SocketException) { }
            _socket.Dispose();
            _socket = null;
            Log.Disconnected(_logger);
            this.OnDisconnected?.Invoke(this, null!);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task SendAsync(IPacket packet, CancellationToken ct = default) => this.SendAsync(packet, null, ct);

    /// <summary>Sends a packet asynchronously with an optional encryption override.</summary>
    /// <param name="packet">The packet to serialize and send.</param>
    /// <param name="encrypt">A value that overrides packet encryption when provided.</param>
    /// <param name="ct">The token to observe while sending.</param>
    public async Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);

        // Rent a buffer, serialize the packet, and delegate sending to FRAME_SENDER
        using BufferLease lease = BufferLease.Rent(packet.Length);
        lease.CommitLength(packet.Serialize(lease.SpanFull));
        _ = await _sender.SendAsync(lease.Memory, encrypt, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default) => _sender.SendAsync(payload, null, ct);

    /// <summary>
    /// Handles messages received by <see cref="FRAME_READER"/>.
    /// </summary>
    private void HandleReceiveMessage(BufferLease lease)
    {
        try
        {
            // First notify synchronous subscribers
            this.OnMessageReceived?.Invoke(this, lease);

            // Then notify asynchronous subscriber if present
            if (this.OnMessageAsync is { } handler)
            {
                // Run async handler in background and ensure lease disposal in finally block
                _ = Task.Run(async () =>
                {
                    try { await handler(lease.Memory).ConfigureAwait(false); }
                    catch (Exception ex) { Log.AsyncHandlerFaulted(_logger, ex); }
                    finally { lease.Dispose(); }
                });
            }
            else
            {
                // No async handler, dispose now
                lease.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.MessageDeliveryFaulted(_logger, ex);
            lease.Dispose();
        }
    }

    private void HandleError(Exception ex)
    {
        Log.TransportError(_logger, ex);
        this.OnDisconnected?.Invoke(this, ex);
        _ = this.DisconnectAsync();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = this.DisconnectAsync();
        _sender.Dispose();
        _reader.Dispose();
        GC.SuppressFinalize(this);
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "[TCP] Connected to {Host}:{Port}")]
        public static partial void Connected(ILogger? logger, string host, int port);

        [LoggerMessage(2, LogLevel.Error, "[TCP] Failed to connect to {Host}:{Port}")]
        public static partial void ConnectFailed(ILogger? logger, string host, int port, Exception ex);

        [LoggerMessage(3, LogLevel.Information, "[TCP] Disconnected.")]
        public static partial void Disconnected(ILogger? logger);

        [LoggerMessage(4, LogLevel.Error, "[TCP] Async handler faulted.")]
        public static partial void AsyncHandlerFaulted(ILogger? logger, Exception ex);

        [LoggerMessage(5, LogLevel.Error, "[TCP] Message delivery faulted.")]
        public static partial void MessageDeliveryFaulted(ILogger? logger, Exception ex);

        [LoggerMessage(6, LogLevel.Error, "[TCP] Transport error occurred.")]
        public static partial void TransportError(ILogger? logger, Exception ex);
    }
}
