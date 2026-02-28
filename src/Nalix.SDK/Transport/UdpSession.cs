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
using Nalix.Framework.Identifiers;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Internal;

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
    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the 7-byte session token (Snowflake) used to identify this session on the UDP channel.
    /// </summary>
    public Snowflake? SessionToken
    {
        get => _sessionToken;
        set => _sessionToken = value;
    }

    /// <inheritdoc/>
    public override TransportOptions Options { get; }

    /// <inheritdoc/>
    public override IPacketRegistry Catalog { get; }

    /// <inheritdoc/>
    public override bool IsConnected => _socket != null && Volatile.Read(ref _disposed) == 0;

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

            // "Connect" the UDP socket to the remote endpoint so we can use Send/Receive instead of SendTo/ReceiveFrom
            await _socket.ConnectAsync(_remoteEndPoint, ct).ConfigureAwait(false);

            _socket.SendBufferSize = this.Options.BufferSize;
            _socket.ReceiveBufferSize = this.Options.BufferSize;

            this.OnConnected?.Invoke(this, EventArgs.Empty);

            // Start background worker for reading datagrams
            _loopCts = new CancellationTokenSource();
            _ = Task.Factory.StartNew(() => this.ReceiveLoopAsync(_loopCts.Token),
                _loopCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            this.OnError?.Invoke(this, ex);
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

        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;

        if (_socket != null)
        {
            _socket.Dispose();
            _socket = null;
            this.OnDisconnected?.Invoke(this, new InvalidOperationException("The UDP session was disconnected."));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        if (!_sessionToken.HasValue)
        {
            throw new NetworkException("SessionToken must be set before sending UDP packets.");
        }

        if (packet.Length + Snowflake.Size > this.Options.MaxUdpDatagramSize)
        {
            throw new NetworkException($"UDP packet too large: {packet.Length + Snowflake.Size} bytes (including token). Max allowed is {this.Options.MaxUdpDatagramSize} bytes. Use TCP for large data.");
        }

        // Step 1: Serialize the IPacket directly into a leasing buffer
        packet.Protocol = Common.Networking.Protocols.ProtocolType.UDP;
        BufferLease src = BufferLease.Rent(packet.Length);
        try
        {
            int written = packet.Serialize(src.Span);
            src.CommitLength(written);

            // Step 2: Transform outbound frame through the shared packet helpers (Compress -> Encrypt).
            this.TransformOutbound(ref src);

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
    public override async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        if (!_sessionToken.HasValue)
        {
            throw new NetworkException("SessionToken must be set before sending UDP packets.");
        }

        // Step 1: Wrap raw payload into a BufferLease
        BufferLease src = BufferLease.Rent(payload.Length);
        try
        {
            payload.Span.CopyTo(src.Span);
            src.CommitLength(payload.Length);

            // Step 2: Transform
            this.TransformOutbound(ref src);

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
            return;
        }

        try
        {
            // Since we're "connected", we use SendAsync
            _ = await _socket.SendAsync(data, SocketFlags.None, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            this.OnError?.Invoke(this, ex);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_socket == null)
        {
            return;
        }

        int bufferSize = this.Options.BufferSize;

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
            catch (Exception ex)
            {
                BufferLease.ByteArrayPool.Return(rawBuffer);
                if (!ct.IsCancellationRequested)
                {
                    this.OnError?.Invoke(this, ex);
                }
                break;
            }

            if (received <= 0)
            {
                BufferLease.ByteArrayPool.Return(rawBuffer);
                continue;
            }

            // Receive raw datagram — for UDP, we receive the packet directly [Flags + Payload]
            // (Server-to-Client UDP does not include the 7-byte token)
            BufferLease datagram = BufferLease.TakeOwnership(rawBuffer, 0, received);

            try
            {
                // Transform inbound frame through the shared packet helpers (Decrypt -> Decompress).
                this.TransformInbound(ref datagram);
                this.OnMessageReceived?.Invoke(this, datagram);
            }
            finally
            {
                datagram.Dispose();
            }
        }
    }

    #endregion Internal

    #region Transformation

    private void TransformOutbound(ref BufferLease src)
        => PacketFrameTransforms.TransformOutbound(ref src, this.Options);

    private void TransformInbound(ref BufferLease lease)
        => PacketFrameTransforms.TransformInbound(ref lease, this.Options.Secret);

    #endregion Transformation

    #region Dispose

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = this.DisconnectAsync();
        GC.SuppressFinalize(this);
    }

    #endregion Dispose
}
