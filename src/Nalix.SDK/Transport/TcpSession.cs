// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a TCP transport session built on <see cref="FrameReader"/> and <see cref="FrameSender"/>.
/// </summary>
public class TcpSession : TransportSession
{
    #region Fields

    // Low-level components for reading and sending frames
    private readonly FrameSender _sender;
    private readonly FrameReader _reader;

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private Socket? _socket;
    private CancellationTokenSource? _loopCts;
    private int _disposed;

    // Sequential dispatcher for async handlers (BUG-10)
    private System.Threading.Channels.Channel<Func<Task>>? _asyncQueue;


    #endregion Fields

    #region Properties

    /// <summary>Gets the fixed framing header size in bytes.</summary>
    public const int HeaderSize = 2;

    /// <inheritdoc/>
    public override TransportOptions Options { get; }

    /// <inheritdoc/>
    public override IPacketRegistry Catalog { get; }

    /// <inheritdoc/>
    public override bool IsConnected => _socket?.Connected == true && Volatile.Read(ref _disposed) == 0;

    #endregion Properties

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
    public TcpSession(TransportOptions options, IPacketRegistry catalog)
    {
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
        this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        // Initialize frame helpers with a factory to get the latest socket instance
        _sender = new FrameSender(() => _socket!, options, this.HandleError);
        _reader = new FrameReader(() => _socket!, options, this.HandleReceiveMessage, this.HandleError);
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(TcpSession));

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string effectiveHost = string.IsNullOrWhiteSpace(host) ? this.Options.Address : host;
            ushort effectivePort = port ?? this.Options.Port;

            // Ensure single connection at a time
            if (this.IsConnected)
            {
                await this.DisconnectInternalAsync().ConfigureAwait(false);
            }

            // Initialize socket with NoDelay to reduce latency
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (this.Options.ConnectTimeoutMillis > 0)
            {
                connectCts.CancelAfter(TimeSpan.FromMilliseconds(this.Options.ConnectTimeoutMillis));
            }

            await _socket.ConnectAsync(effectiveHost, effectivePort, connectCts.Token).ConfigureAwait(false);
            this.OnConnected?.Invoke(this, EventArgs.Empty);

            // Start background worker for reading frames
            _loopCts = new CancellationTokenSource();

            // Initialize sequential async dispatcher with backpressure (SEC-56)
            _asyncQueue = System.Threading.Channels.Channel.CreateBounded<Func<Task>>(
                new System.Threading.Channels.BoundedChannelOptions(this.Options.AsyncQueueCapacity)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
                });

            _ = Task.Run(() => this.ProcessAsyncQueueAsync(_loopCts.Token), ct);
            _ = Task.Run(() => _reader.ReceiveLoopAsync(_loopCts.Token), CancellationToken.None);
        }
        catch (Exception ex)
        {
            await this.DisconnectInternalAsync().ConfigureAwait(false);
            this.OnError?.Invoke(this, ex);
            throw new NetworkException($"Connection failed: {ex.Message}", ex);
        }
        finally
        {
            _ = _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public override async Task DisconnectAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await this.DisconnectInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _connectionLock.Release();
        }
    }

    private Task DisconnectInternalAsync()
    {
        CancellationTokenSource? loopCts = Interlocked.Exchange(ref _loopCts, null);
        Socket? socket = Interlocked.Exchange(ref _socket, null);

        try
        {
            loopCts?.Cancel();
        }
        catch (ObjectDisposedException) { }
        finally
        {
            loopCts?.Dispose();
        }

        if (socket is not null)
        {
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            socket.Dispose();
            this.OnDisconnected?.Invoke(this, new NetworkException("The TCP session was disconnected."));
        }

        // Close async queue
        _ = (_asyncQueue?.Writer.TryComplete());
        _asyncQueue = null;

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

        packet.Protocol = Common.Networking.Protocols.ProtocolType.TCP;

        // Rent a buffer, serialize the packet, and delegate sending to FrameSender
        using BufferLease lease = BufferLease.Rent(packet.Length);
        lease.CommitLength(packet.Serialize(lease.SpanFull));
        bool sent = await _sender.SendAsync(lease.Memory, encrypt, ct).ConfigureAwait(false);
        if (!sent)
        {
            throw new NetworkException("Failed to send TCP packet: the frame was not delivered to the socket.");
        }
    }

    /// <inheritdoc/>
    public override Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default) => _sender.SendAsync(payload, null, ct);

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = this.DisconnectInternalAsync();
        _sender.Dispose();
        _reader.Dispose();
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private

    private void HandleError(Exception ex)
    {
        this.OnError?.Invoke(this, ex);
        _ = this.DisconnectAsync();
    }

    /// <summary>
    /// Handles messages received by <see cref="FrameReader"/>.
    /// </summary>
    private void HandleReceiveMessage(IBufferLease lease)
    {
        Func<ReadOnlyMemory<byte>, Task>? asyncHandler = this.OnMessageAsync;
        EventHandler<IBufferLease>? syncHandler = this.OnMessageReceived;

        try
        {
            ReadOnlyMemory<byte>? asyncPayload = null;
            System.Threading.Channels.ChannelWriter<Func<Task>>? writer = _asyncQueue?.Writer;

            if (asyncHandler is not null && syncHandler is not null)
            {
                asyncPayload = lease.Memory.ToArray();
            }

            syncHandler?.Invoke(this, lease);

            if (asyncHandler is not null && writer is not null)
            {
                if (asyncPayload is { } copiedPayload)
                {
                    if (!writer.TryWrite(async () =>
                        {
                            try { await asyncHandler(copiedPayload).ConfigureAwait(false); }
                            catch (Exception ex) { this.OnError?.Invoke(this, ex); }
                        }))
                    {
                        this.OnError?.Invoke(this, new NetworkException("Async handler queue saturated; frame dropped."));
                    }
                    return;
                }

                lease.Retain();
                if (!writer.TryWrite(async () =>
                {
                    try { await asyncHandler(lease.Memory).ConfigureAwait(false); }
                    catch (Exception ex) { this.OnError?.Invoke(this, ex); }
                    finally { lease.Dispose(); }
                }))
                {
                    this.OnError?.Invoke(this, new NetworkException("Async handler queue saturated; frame dropped."));
                    lease.Dispose();
                }
                return;
            }
        }
        catch (Exception ex)
        {
            this.OnError?.Invoke(this, ex);
        }
    }

    private async Task ProcessAsyncQueueAsync(CancellationToken ct)
    {
        System.Threading.Channels.ChannelReader<Func<Task>>? reader = _asyncQueue?.Reader;
        if (reader is null)
        {
            return;
        }

        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out Func<Task>? work))
                {
                    try
                    {
                        await work().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        this.OnError?.Invoke(this, ex);
                    }
                }
            }
        }
        finally
        {
            // Drain the queue to ensure any enqueued work (with finally { lease.Dispose() })
            // is executed so we don't leak leases on shutdown.
            while (reader.TryRead(out Func<Task>? work))
            {
                // We don't await because we're shutting down, but we must start it 
                // so the 'finally' blocks inside 'work()' run.
                _ = work();
            }
        }
    }

    #endregion Private
}
