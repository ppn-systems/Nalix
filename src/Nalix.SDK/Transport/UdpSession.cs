// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Codec.Memory;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Internal;

#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a high-performance UDP transport session supporting 8-byte session token authentication.
/// </summary>
/// <remarks>
/// <para>
/// Datagram layout for outbound packets: <c>[SessionToken (8 bytes) | Payload ...]</c>.
/// </para>
/// <para>
/// This class uses <see cref="UdpFrameSender"/> and <see cref="UdpFrameReader"/> 
/// to separate low-level frame handling, making the code consistent with <see cref="TcpSession"/>.
/// </para>
/// <para>
/// UDP does NOT perform framing or fragmentation.
/// </para>
/// </remarks>
public class UdpSession : TransportSession
{
    #region Fields

    // Low-level components for reading and sending datagrams
    private readonly UdpFrameSender _sender;
    private readonly UdpFrameReader _reader;

    private Socket? _socket;
    private IPEndPoint? _remoteEndPoint;
    private CancellationTokenSource? _loopCts;
    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the 8-byte session token (Snowflake) used to identify this session on the UDP channel.
    /// </summary>
    public ulong SessionToken
    {
        get => this.Options.SessionToken;
        set => this.Options.SessionToken = value;
    }

    /// <inheritdoc/>
    public override TransportOptions Options { get; }

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
    public UdpSession(TransportOptions options)
    {
        this.Options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize frame helpers with a factory to get the latest socket instance
        _sender = new UdpFrameSender(() => _socket!, options, this.HandleError);
        _reader = new UdpFrameReader(
            () => _socket!,
            options,
            this.HandleReceiveMessage,
            this.OnMessageAsync,           // pass async handler (can be changed at runtime)
            this.HandleError);
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }

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
            _socket = new Socket(_remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
            {
                SendBufferSize = this.Options.BufferSize,
                ReceiveBufferSize = this.Options.BufferSize
            };

            // Apply connect timeout if configured
            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (this.Options.ConnectTimeoutMillis > 0)
            {
                connectCts.CancelAfter(TimeSpan.FromMilliseconds(this.Options.ConnectTimeoutMillis));
            }

            await _socket.ConnectAsync(_remoteEndPoint, connectCts.Token).ConfigureAwait(false);

            _loopCts = new CancellationTokenSource();

            // Start background receive loop
            _ = Task.Factory.StartNew(() => _reader.ReceiveLoopAsync(_loopCts.Token),
                _loopCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

            this.OnConnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            await this.DisconnectInternalAsync().ConfigureAwait(false);
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

        return this.DisconnectInternalAsync();
    }

    private async Task DisconnectInternalAsync()
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _loopCts, null);
        Socket? socket = Interlocked.Exchange(ref _socket, null);

        try
        {
            await (cts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { /* ignored */ }

        if (socket != null)
        {
            try
            {
                socket.Dispose();
            }
            catch (ObjectDisposedException) { /* ignored */ }

            this.OnDisconnected?.Invoke(this, new NetworkException("The UDP session was disconnected."));
        }

        cts?.Dispose();
    }

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, CancellationToken ct = default)
        => await this.SendAsync(packet, null, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet is IPacketHeader h)
        {
            h.Flags = (h.Flags & ~PacketFlags.RELIABLE) | PacketFlags.UNRELIABLE;
        }

        using BufferLease lease = BufferLease.Rent(packet.Length);
        int written = packet.Serialize(lease.SpanFull);
        lease.CommitLength(written);

        bool sent = await _sender.SendAsync(lease.Memory, encrypt, ct).ConfigureAwait(false);

        if (!sent)
        {
            throw new NetworkException("Failed to send UDP packet: the datagram was not delivered to the socket.");
        }
    }

    /// <inheritdoc/>
    public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
        => _sender.SendAsync(payload, encrypt, ct);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing || Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = this.DisconnectInternalAsync();
        _sender.Dispose();
        _reader.Dispose();
        _socket?.Dispose();
        _loopCts?.Dispose();
    }

    #endregion APIs

    #region Private Handlers

    private void HandleError(Exception ex)
    {
        this.OnError?.Invoke(this, ex);
        _ = this.DisconnectAsync();
    }

    /// <summary>
    /// Handles messages received by <see cref="UdpFrameReader"/>.
    /// </summary>
    private void HandleReceiveMessage(IBufferLease lease)
    {
        try
        {
            // Direct synchronous dispatch (hot path)
            this.OnMessageReceived?.Invoke(this, lease);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.OnError?.Invoke(this, ex);
        }
    }

    #endregion Private
}
