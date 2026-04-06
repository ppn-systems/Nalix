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
using Nalix.Framework.DataFrames;
using Nalix.Framework.Extensions;
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

    /// <inheritdoc/>
    public override Nalix.Common.Networking.IProtocol? Protocol { get; set; }

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
        using BufferLease src = BufferLease.Rent(packet.Length);
        int written = packet.Serialize(src.Span);
        src.CommitLength(written);

        // Step 2: Transform (Compress -> Encrypt) using the built-in packet header
        BufferLease transformed = this.TransformOutbound(src);

        try
        {
            // Step 3: Check MTU (Token + Packet)
            if (transformed.Length + Snowflake.Size > this.Options.MaxUdpDatagramSize)
            {
                throw new NetworkException($"UDP packet too large after transformation: {transformed.Length + Snowflake.Size} bytes. Max allowed is {this.Options.MaxUdpDatagramSize} bytes.");
            }

            // Step 4: Final Envelope [Token + Packet]
            using BufferLease finalLease = BufferLease.Rent(Snowflake.Size + transformed.Length);
            _ = _sessionToken.Value.TryWriteBytes(finalLease.SpanFull[..Snowflake.Size]);
            transformed.Span.CopyTo(finalLease.SpanFull[Snowflake.Size..]);
            finalLease.CommitLength(Snowflake.Size + transformed.Length);

            await this.SendAsyncInternal(finalLease.Memory, ct).ConfigureAwait(false);
        }
        finally
        {
            if (transformed != src)
            {
                transformed.Dispose();
            }
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
        using BufferLease src = BufferLease.Rent(payload.Length);
        payload.Span.CopyTo(src.Span);
        src.CommitLength(payload.Length);

        // Step 2: Transform
        BufferLease transformed = this.TransformOutbound(src);

        try
        {
            if (transformed.Length + Snowflake.Size > this.Options.MaxUdpDatagramSize)
            {
                throw new NetworkException($"UDP payload too large after transformation: {transformed.Length + Snowflake.Size} bytes. Max allowed is {this.Options.MaxUdpDatagramSize} bytes.");
            }

            using BufferLease finalLease = BufferLease.Rent(Snowflake.Size + transformed.Length);
            _ = _sessionToken.Value.TryWriteBytes(finalLease.SpanFull[..Snowflake.Size]);
            transformed.Span.CopyTo(finalLease.SpanFull[Snowflake.Size..]);
            finalLease.CommitLength(Snowflake.Size + transformed.Length);

            await this.SendAsyncInternal(finalLease.Memory, ct).ConfigureAwait(false);
        }
        finally
        {
            if (transformed != src)
            {
                transformed.Dispose();
            }
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

            try
            {
                int received = await _socket.ReceiveAsync(rawBuffer, SocketFlags.None, ct).ConfigureAwait(false);
                if (received == 0)
                {
                    continue;
                }

                // Receive raw datagram — for UDP, we receive the packet directly [Flags + Payload]
                // (Server-to-Client UDP does not include the 7-byte token)
                BufferLease datagram = BufferLease.TakeOwnership(rawBuffer, 0, received);

                // Transform (Decrypt -> Decompress)
                BufferLease transformed = this.TransformInbound(datagram);

                try
                {
                    this.OnMessageReceived?.Invoke(this, transformed);
                }
                finally
                {
                    transformed.Dispose();
                }
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
        }
    }

    #endregion Internal

    #region Transformation

    private BufferLease TransformOutbound(BufferLease src)
    {
        bool doEncrypt = this.Options.EncryptionEnabled;
        bool doCompress = this.Options.CompressionEnabled && (src.Length - FrameTransformer.Offset) >= this.Options.CompressionThreshold;

        BufferLease current = src;
        current.Retain();

        try
        {
            if (doCompress)
            {
                BufferLease next = BufferLease.Rent(FrameTransformer.GetMaxCompressedSize(current.Length - FrameTransformer.Offset) + FrameTransformer.Offset);
                try
                {
                    FrameTransformer.Compress(current, next);
                    next.Span.WriteFlagsLE(next.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));
                    current.Dispose();
                    current = next;
                }
                catch { next.Dispose(); throw; }
            }

            if (doEncrypt)
            {
                BufferLease next = BufferLease.Rent(FrameTransformer.GetMaxCiphertextSize(this.Options.Algorithm, current.Length - FrameTransformer.Offset) + FrameTransformer.Offset);
                try
                {
                    FrameTransformer.Encrypt(current, next, this.Options.Secret, this.Options.Algorithm);
                    next.Span.WriteFlagsLE(next.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
                    current.Dispose();
                    current = next;
                }
                catch { next.Dispose(); throw; }
            }

            return current;
        }
        catch (System.Exception)
        {
            current.Dispose();
            throw;
        }
    }

    private BufferLease TransformInbound(BufferLease lease)
    {
        BufferLease current = lease;
        current.Retain();

        try
        {
            PacketFlags flags = current.Span.ReadFlagsLE();

            if (flags.HasFlag(PacketFlags.ENCRYPTED))
            {
                BufferLease decrypted = BufferLease.Rent(FrameTransformer.GetPlaintextLength(current.Span) + FrameTransformer.Offset);
                try
                {
                    FrameTransformer.Decrypt(current, decrypted, this.Options.Secret);
                    // Do NOT write flags back yet if we expect more transformations
                    // The flags are part of the original header
                    current.Dispose();
                    current = decrypted;
                    flags = current.Span.ReadFlagsLE();
                }
                catch { decrypted.Dispose(); throw; }
            }

            if (flags.HasFlag(PacketFlags.COMPRESSED))
            {
                BufferLease decompressed = BufferLease.Rent(FrameTransformer.GetDecompressedLength(current.Span) + FrameTransformer.Offset);
                try
                {
                    FrameTransformer.Decompress(current, decompressed);
                    current.Dispose();
                    current = decompressed;
                }
                catch { decompressed.Dispose(); throw; }
            }

            return current;
        }
        catch (System.Exception)
        {
            current.Dispose();
            throw;
        }
        finally
        {
            lease.Dispose();
        }
    }

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
