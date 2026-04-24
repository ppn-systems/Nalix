// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a high-performance UDP transport session supporting 7-byte session token authentication.
/// </summary>
/// <remarks>
/// Datagram layout for outbound packets: <c>[SessionToken (7 bytes) | Payload ...]</c>.
/// Inbound packets from the server are treated as raw payloads.
/// </remarks>
public class UdpSession : TransportSession
{
    #region Fields

    private Socket? _socket;
    private IPEndPoint? _remoteEndPoint;
    private Snowflake? _sessionToken;
    private CancellationTokenSource? _loopCts;
    private System.Threading.Channels.Channel<Func<Task>>? _asyncQueue;
    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the 7-byte session token (Snowflake) used to identify this session on the UDP channel.
    /// </summary>
    public Snowflake? SessionToken
    {
        get => _sessionToken ?? (this.Options.SessionToken.IsEmpty ? null : this.Options.SessionToken);
        set
        {
            _sessionToken = value;
            this.Options.SessionToken = value ?? Snowflake.Empty;
        }
    }

    /// <inheritdoc/>
    public override TransportOptions Options { get; }

    /// <inheritdoc/>
    public override IPacketRegistry Catalog { get; }

    /// <inheritdoc/>
    public override bool IsConnected => _socket != null && Volatile.Read(ref _disposed) == 0;

    /// <summary>Occurs when a complete frame is received and decoded asynchronously.</summary>
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageAsync;

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

    #endregion Events

    #region Constructor

    /// <summary>Initializes a new instance of the <see cref="UdpSession"/> class.</summary>
    /// <param name="options">The transport options for this session.</param>
    /// <param name="catalog">The packet registry used to resolve packet metadata.</param>
    public UdpSession(TransportOptions options, IPacketRegistry catalog)
    {
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
        this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        string effectiveHost = string.IsNullOrWhiteSpace(host) ? this.Options.Address : host;
        ushort effectivePort = port ?? this.Options.Port;

        if (this.IsConnected)
        {
            await this.DisconnectAsync().ConfigureAwait(false);
        }

        try
        {
            // Resolve the remote endpoint
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(effectiveHost, ct).ConfigureAwait(false);
            if (addresses.Length == 0)
            {
                throw new NetworkException($"Could not resolve host: {effectiveHost}");
            }

            _remoteEndPoint = new IPEndPoint(addresses[0], effectivePort);

            // Initialize UDP socket
            _socket = new Socket(_remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _sessionToken = this.Options.SessionToken.IsEmpty ? null : this.Options.SessionToken;

            // BUG-62: Apply ConnectTimeoutMillis from options
            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (this.Options.ConnectTimeoutMillis > 0)
            {
                connectCts.CancelAfter(TimeSpan.FromMilliseconds(this.Options.ConnectTimeoutMillis));
            }

            // "Connect" the UDP socket to the remote endpoint
            await _socket.ConnectAsync(_remoteEndPoint, connectCts.Token).ConfigureAwait(false);

            // SEC-59: Use a bounded channel for async handlers to prevent memory exhaustion
            _asyncQueue = System.Threading.Channels.Channel.CreateBounded<Func<Task>>(
                new System.Threading.Channels.BoundedChannelOptions(this.Options.AsyncQueueCapacity)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
                });

            // Start background workers
            _loopCts = new CancellationTokenSource();
            Task asyncQueueTask = Task.Run(() => this.ProcessAsyncQueueAsync(_loopCts.Token), _loopCts.Token);
            this.ObserveBackgroundTask(asyncQueueTask, nameof(ProcessAsyncQueueAsync));

            _socket.SendBufferSize = this.Options.BufferSize;
            _socket.ReceiveBufferSize = this.Options.BufferSize;

            this.OnConnected?.Invoke(this, EventArgs.Empty);

            Task receiveLoopTask = Task.Factory.StartNew(() => this.ReceiveLoopAsync(_loopCts.Token),
                _loopCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            this.ObserveBackgroundTask(receiveLoopTask, nameof(ReceiveLoopAsync));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.OnError?.Invoke(this, ex);
            await this.DisconnectAsync().ConfigureAwait(false);
            throw new NetworkException($"UDP Connection failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public override Task DisconnectAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return Task.CompletedTask;
        }

        return this.DisconnectInternalAsync();
    }

    private Task DisconnectInternalAsync()
    {

        // BUG-27 fix: Cancel CTS first, then dispose socket, then dispose CTS.
        // Order matters: the receive loop checks CancellationToken before socket ops,
        // so cancelling first lets it exit cleanly before we dispose the socket.
        CancellationTokenSource? cts = Interlocked.Exchange(ref _loopCts, null);
        if (cts is not null)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException ex) when (Volatile.Read(ref _disposed) == 1)
            {
                this.OnError?.Invoke(this, ex);
            }
            catch (ObjectDisposedException ex)
            {
                this.OnError?.Invoke(this, ex);
            }
        }

        Socket? socket = Interlocked.Exchange(ref _socket, null);
        if (socket != null)
        {
            try { socket.Dispose(); }
            catch (ObjectDisposedException ex) when (Volatile.Read(ref _disposed) == 1)
            {
                this.OnError?.Invoke(this, ex);
            }
            catch (ObjectDisposedException ex)
            {
                this.OnError?.Invoke(this, ex);
            }

            this.OnDisconnected?.Invoke(this, new NetworkException("The UDP session was disconnected."));
        }

        // Dispose CTS last — after the socket is gone and the loop is dead.
        if (cts is not null)
        {
            try { cts.Dispose(); }
            catch (ObjectDisposedException ex) when (Volatile.Read(ref _disposed) == 1)
            {
                this.OnError?.Invoke(this, ex);
            }
            catch (ObjectDisposedException ex)
            {
                this.OnError?.Invoke(this, ex);
            }
        }

        _ = (_asyncQueue?.Writer.TryComplete());
        _asyncQueue = null;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, CancellationToken ct = default) => await this.SendAsync(packet, null, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        if (!_sessionToken.HasValue)
        {
            throw new NetworkException("SessionToken must be set before sending UDP packets.");
        }

        packet.Flags = (packet.Flags & ~PacketFlags.RELIABLE) | PacketFlags.UNRELIABLE;
        int packetLength = packet.Length;

        if (packetLength + Snowflake.Size > this.Options.MaxUdpDatagramSize)
        {
            throw new NetworkException($"UDP packet too large: {packetLength + Snowflake.Size} bytes (including token). Max allowed is {this.Options.MaxUdpDatagramSize} bytes. Use TCP for large data.");
        }

        // Step 1: Serialize the IPacket directly into a leasing buffer
        IBufferLease src = BufferLease.Rent(packetLength);
        try
        {
            int written = packet.Serialize(src.Span);
            src.CommitLength(written);

            // Step 2: Transform outbound frame through the shared packet helpers (Compress -> Encrypt).
            FramePipeline.ProcessOutbound(
                ref src,
                this.Options.CompressionEnabled,
                this.Options.CompressionThreshold,
                encrypt ?? this.Options.EncryptionEnabled,
                this.Options.Secret.AsSpan(), this.Options.Algorithm);

            // Step 3: Check MTU (Token + Packet)
            if (src.Length + Snowflake.Size > this.Options.MaxUdpDatagramSize)
            {
                throw new NetworkException($"UDP packet too large after transformation: {src.Length + Snowflake.Size} bytes. Max allowed is {this.Options.MaxUdpDatagramSize} bytes.");
            }

            // Step 4: Final Envelope [Token + Packet]
            using BufferLease finalLease = BufferLease.Rent(Snowflake.Size + src.Length);
            _ = _sessionToken.Value.TryWriteBytes(finalLease.SpanFull[..Snowflake.Size]);
            src.Span.CopyTo(finalLease.SpanFull[Snowflake.Size..]);
            finalLease.CommitLength(Snowflake.Size + src.Length);

            await this.SendAsyncInternal(finalLease.Memory, ct).ConfigureAwait(false);
        }
        finally
        {
            src.Dispose();
        }
    }

    /// <inheritdoc/>
    public override async Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        if (!_sessionToken.HasValue)
        {
            throw new NetworkException("SessionToken must be set before sending UDP packets.");
        }

        // Step 1: Wrap raw payload into a BufferLease
        IBufferLease src = BufferLease.Rent(payload.Length);
        try
        {
            payload.Span.CopyTo(src.Span);
            src.CommitLength(payload.Length);

            // Step 2: Transform
            FramePipeline.ProcessOutbound(
                ref src,
                this.Options.CompressionEnabled,
                this.Options.CompressionThreshold,
                encrypt ?? this.Options.EncryptionEnabled,
                this.Options.Secret.AsSpan(), this.Options.Algorithm);

            if (src.Length + Snowflake.Size > this.Options.MaxUdpDatagramSize)
            {
                throw new NetworkException($"UDP payload too large after transformation: {src.Length + Snowflake.Size} bytes. Max allowed is {this.Options.MaxUdpDatagramSize} bytes.");
            }

            using BufferLease finalLease = BufferLease.Rent(Snowflake.Size + src.Length);
            _ = _sessionToken.Value.TryWriteBytes(finalLease.SpanFull[..Snowflake.Size]);
            src.Span.CopyTo(finalLease.SpanFull[Snowflake.Size..]);
            finalLease.CommitLength(Snowflake.Size + src.Length);

            await this.SendAsyncInternal(finalLease.Memory, ct).ConfigureAwait(false);
        }
        finally
        {
            src.Dispose();
        }
    }

    #endregion APIs

    #region Internal

    private async Task SendAsyncInternal(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_socket == null)
        {
            throw new NetworkException("Cannot send UDP data: the session is not connected.");
        }

        try
        {
            // Since we're "connected", we use SendAsync
            _ = await _socket.SendAsync(data, SocketFlags.None, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.OnError?.Invoke(this, ex);
            _ = this.DisconnectAsync();
            throw;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_socket == null)
        {
            return;
        }

        int bufferSize = this.Options.MaxUdpDatagramSize;

        while (!ct.IsCancellationRequested)
        {
            byte[] rawBuffer = BufferLease.ByteArrayPool.Rent(bufferSize);
            int received;

            try
            {
                received = await _socket.ReceiveAsync(rawBuffer, SocketFlags.None, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                BufferLease.ByteArrayPool.Return(rawBuffer);
                break;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                BufferLease.ByteArrayPool.Return(rawBuffer);
                if (!ct.IsCancellationRequested)
                {
                    this.OnError?.Invoke(this, ex);
                    _ = this.DisconnectAsync();
                }
                break;
            }

            if (received <= 0)
            {
                BufferLease.ByteArrayPool.Return(rawBuffer);
                continue;
            }

            try
            {
                // Receive raw datagram — for UDP, we receive the packet directly [Flags + Payload]
                // (Server-to-Client UDP does not include the 7-byte token)
                IBufferLease datagram = BufferLease.TakeOwnership(rawBuffer, 0, received);

                try
                {
                    // Transform inbound frame through the shared packet helpers (Decrypt -> Decompress).
                    FramePipeline.ProcessInbound(ref datagram, this.Options.Secret.AsSpan(), this.Options.Algorithm);

                    Func<ReadOnlyMemory<byte>, Task>? asyncHandler = this.OnMessageAsync;
                    EventHandler<IBufferLease>? syncHandler = this.OnMessageReceived;

                    System.Threading.Channels.ChannelWriter<Func<Task>>? writer = _asyncQueue?.Writer;
                    if (asyncHandler is not null && writer is not null && syncHandler is not null)
                    {
                        // Copy payload for async handler to prevent race with sync handler's implicit lifecycle.
                        byte[] copy = datagram.Memory.ToArray();
                        if (!writer.TryWrite(async () =>
                        {
                            try { await asyncHandler(copy).ConfigureAwait(false); }
                            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { this.OnError?.Invoke(this, ex); }
                        }))
                        {
                            this.OnError?.Invoke(this, new NetworkException("Async handler queue saturated; dual-mode frame dropped."));
                        }
                    }
                    else if (asyncHandler is not null && writer is not null)
                    {
                        datagram.Retain();
                        if (!writer.TryWrite(async () =>
                        {
                            try { await asyncHandler(datagram.Memory).ConfigureAwait(false); }
                            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { this.OnError?.Invoke(this, ex); }
                            finally
                            {
                                datagram.Dispose();
                            }
                        }))
                        {
                            datagram.Dispose();
                        }
                    }

                    syncHandler?.Invoke(this, datagram);
                }
                finally
                {
                    datagram.Dispose();
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (!ct.IsCancellationRequested)
                {
                    this.OnError?.Invoke(this, ex);
                }
            }
        }
    }

    #endregion Internal

    #region Dispose

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = this.DisconnectInternalAsync();
        GC.SuppressFinalize(this);
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
                    try { await work().ConfigureAwait(false); }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { this.OnError?.Invoke(this, ex); }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException ex)
        {
            this.OnError?.Invoke(this, ex);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { this.OnError?.Invoke(this, ex); }
        finally
        {
            while (reader.TryRead(out Func<Task>? work))
            {
                Task task;
                try
                {
                    task = work();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    this.OnError?.Invoke(this, ex);
                    continue;
                }

                this.ObserveBackgroundTask(task, "AsyncQueueDrain");
            }
        }
    }

    private void ObserveBackgroundTask(Task task, string operation)
    {
        _ = task.ContinueWith(static (t, state) =>
        {
            if (state is not Tuple<UdpSession, string> payload)
            {
                return;
            }

            UdpSession self = payload.Item1;
            string op = payload.Item2;
            Exception? error = t.Exception?.GetBaseException();
            if (error is not null && Volatile.Read(ref self._disposed) == 0)
            {
                self.OnError?.Invoke(self, new NetworkException($"Background operation '{op}' failed.", error));
            }
        }, Tuple.Create(this, operation), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    #endregion
}
