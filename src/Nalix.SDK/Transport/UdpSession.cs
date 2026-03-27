// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Extensions;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Random;
using Nalix.Framework.Security.Hashing;
using Nalix.Framework.Time;
using Nalix.SDK.Configuration;

namespace Nalix.SDK.Transport;

/// <summary>
/// Represents a UDP client session for raw datagram transport.
/// </summary>
/// <remarks>
/// Unlike <see cref="TcpSession"/>, this class does not add a length prefix because
/// <c>Nalix.Network</c> UDP transport sends raw datagrams. When <see cref="SessionId"/>
/// and a 32-byte secret are available, the session can append the UDP authentication
/// trailer expected by <c>UdpListenerBase</c>.
/// </remarks>
[DebuggerStepThrough]
[DebuggerDisplay("Connected = {IsConnected}, Remote = {RemoteEndPoint}")]
[EditorBrowsable(EditorBrowsableState.Never)]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class UdpSession : IClientConnection, IAsyncDisposable
{
    #region Constants

    private const int TimestampSize = sizeof(long);
    private const int NonceSize = sizeof(ulong);
    private const int AuthenticationTagSize = Poly1305.TagSize;
    private const int AuthenticationMetadataSize = Snowflake.Size + TimestampSize + NonceSize + AuthenticationTagSize;

    #endregion Constants

    #region Fields

    private readonly ILogger? _logger;
    private readonly IThreadDispatcher _dispatcher;
    private readonly Lock _sync = new();

    private Socket? _socket;
    private CancellationTokenSource? _loopCts;
    private Task? _receiveTask;

    private string? _host;
    private ushort? _port;
    private int _disposed;
    private int _connected;
    private int _reconnecting;
    private int _hasEverConnected;

    private long _bytesSent;
    private long _bytesReceived;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpSession"/> class using
    /// services resolved from <see cref="ConfigurationManager"/> and <see cref="InstanceManager"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an <see cref="IPacketRegistry"/> instance is not available.
    /// </exception>
    public UdpSession()
    {
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        _dispatcher = InstanceManager.Instance.GetExistingInstance<IThreadDispatcher>() ?? new InlineDispatcher();
        this.Catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
            ?? throw new InvalidOperationException("IPacketRegistry instance not found.");

        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Options.Validate();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpSession"/> class with an explicit packet registry
    /// and optional common services.
    /// </summary>
    /// <param name="registry">The packet registry used for packet serialization and deserialization.</param>
    /// <param name="logger">Optional logger override. When null, the logger is resolved from <see cref="InstanceManager"/>.</param>
    /// <param name="dispatcher">Optional dispatcher override. When null, the dispatcher is resolved from <see cref="InstanceManager"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
    public UdpSession(
        IPacketRegistry registry,
        ILogger? logger = null,
        IThreadDispatcher? dispatcher = null)
    {
        _logger = logger ?? InstanceManager.Instance.GetExistingInstance<ILogger>();
        _dispatcher = dispatcher ?? InstanceManager.Instance.GetExistingInstance<IThreadDispatcher>() ?? new InlineDispatcher();
        this.Catalog = registry ?? throw new ArgumentNullException(nameof(registry));
        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Options.Validate();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpSession"/> class with explicit dependencies.
    /// </summary>
    /// <param name="options">The transport options used by this session.</param>
    /// <param name="registry">The packet registry used for packet serialization and deserialization.</param>
    public UdpSession(TransportOptions options, IPacketRegistry registry)
        : this(options, registry, logger: null, dispatcher: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpSession"/> class with explicit transport options,
    /// packet registry, and optional common services.
    /// </summary>
    /// <param name="options">The transport options used by this session.</param>
    /// <param name="registry">The packet registry used for packet serialization and deserialization.</param>
    /// <param name="logger">Optional logger override. When null, the logger is resolved from <see cref="InstanceManager"/>.</param>
    /// <param name="dispatcher">Optional dispatcher override. When null, the dispatcher is resolved from <see cref="InstanceManager"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="registry"/> is null.</exception>
    public UdpSession(
        TransportOptions options,
        IPacketRegistry registry,
        ILogger? logger = null,
        IThreadDispatcher? dispatcher = null)
    {
        _logger = logger ?? InstanceManager.Instance.GetExistingInstance<ILogger>();
        _dispatcher = dispatcher ?? InstanceManager.Instance.GetExistingInstance<IThreadDispatcher>() ?? new InlineDispatcher();
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
        this.Catalog = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    #endregion Constructors

    #region Properties

    /// <inheritdoc/>
    public TransportOptions Options { get; }

    ITransportOptions IClientConnection.Options => this.Options;

    /// <inheritdoc/>
    public IPacketRegistry Catalog { get; }

    /// <summary>
    /// Gets or sets the session identifier used to build the authenticated UDP trailer
    /// expected by <c>Nalix.Network</c> listeners.
    /// </summary>
    public ISnowflake? SessionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether authenticated UDP metadata should be appended
    /// when enough information is available.
    /// </summary>
    public bool UseAuthenticationMetadata { get; set; } = true;

    /// <summary>
    /// Gets the remote endpoint currently associated with this UDP session.
    /// </summary>
    public EndPoint? RemoteEndPoint { get; private set; }

    /// <summary>
    /// Gets the total number of bytes sent.
    /// </summary>
    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total number of bytes received.
    /// </summary>
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <inheritdoc/>
    public bool IsConnected => Volatile.Read(ref _connected) == 1 && Volatile.Read(ref _disposed) == 0;

    /// <summary>
    /// Gets or sets an asynchronous message handler invoked after synchronous event subscribers.
    /// </summary>
    /// <remarks>
    /// The callback receives a copied payload so it can safely outlive the internal receive lease.
    /// </remarks>
    public Func<UdpSession, ReadOnlyMemory<byte>, Task>? OnMessageReceivedAsync { get; set; }

    #endregion Properties

    #region Events

    /// <inheritdoc/>
    public event EventHandler? OnConnected;

    /// <inheritdoc/>
    public event EventHandler<Exception>? OnDisconnected;

    /// <inheritdoc/>
    public event EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public event EventHandler<long>? OnBytesSent;

    /// <inheritdoc/>
    public event EventHandler<long>? OnBytesReceived;

    /// <inheritdoc/>
    public event EventHandler<Exception>? OnError;

    /// <summary>
    /// Occurs when the session reconnects after an error.
    /// </summary>
    public event EventHandler<int>? OnReconnected;

    #endregion Events

    #region Public Methods

    /// <summary>
    /// Copies the authenticated session context from an existing client connection.
    /// </summary>
    /// <param name="source">
    /// The source connection, typically a TCP session that has already completed authentication or key exchange.
    /// </param>
    /// <param name="sessionId">The server-side connection identifier used by UDP authentication.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/> or <paramref name="sessionId"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="sessionId"/> is empty or the source secret is too short for UDP authentication.
    /// </exception>
    /// <remarks>
    /// The source secret is copied into a new array so later changes to the source connection do not
    /// implicitly alter the UDP authentication state.
    /// </remarks>
    public void BindFrom(IClientConnection source, ISnowflake sessionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sessionId);

        if (sessionId.IsEmpty)
        {
            throw new ArgumentException("SessionId must not be empty.", nameof(sessionId));
        }

        byte[] secret = source.Options.Secret;
        if (secret is null || secret.Length < Poly1305.KeySize)
        {
            throw new ArgumentException(
                $"Source connection must expose a secret with at least {Poly1305.KeySize} bytes.",
                nameof(source));
        }

        byte[] secretCopy = new byte[secret.Length];
        Array.Copy(secret, secretCopy, secret.Length);

        this.SessionId = sessionId;
        this.Options.Secret = secretCopy;

        if (!string.IsNullOrWhiteSpace(source.Options.Address))
        {
            this.Options.Address = source.Options.Address;
        }

        if (source.Options.Port != 0)
        {
            this.Options.Port = source.Options.Port;
        }

        _logger?.Info(
            $"[SDK.{nameof(UdpSession)}] Bound UDP auth context from {source.GetType().Name} " +
            $"to {this.Options.Address}:{this.Options.Port} with session={sessionId}.");
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The session has already been disposed.</exception>
    /// <exception cref="ArgumentException">The resolved host is null, empty, or whitespace.</exception>
    /// <exception cref="SocketException">No endpoint could be connected successfully.</exception>
    public async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        string? effectiveHost = string.IsNullOrWhiteSpace(host) ? this.Options.Address : host;
        ushort effectivePort = port ?? this.Options.Port;

        if (string.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new ArgumentException("Host is required.", nameof(host));
        }

        if (this.IsConnected &&
            string.Equals(_host, effectiveHost, StringComparison.OrdinalIgnoreCase) &&
            _port == effectivePort)
        {
            return;
        }

        if (this.IsConnected)
        {
            this.TearDownConnection();
        }

        lock (_sync)
        {
            CancelAndDispose(ref _loopCts);
        }

        using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (this.Options.ConnectTimeoutMillis > 0)
        {
            connectCts.CancelAfter(this.Options.ConnectTimeoutMillis);
        }

        IPAddress[] addresses = IPAddress.TryParse(effectiveHost, out IPAddress? ip)
            ? [ip]
            : await Dns.GetHostAddressesAsync(effectiveHost, connectCts.Token).ConfigureAwait(false);

        Exception? lastEx = null;

        foreach (IPAddress address in addresses)
        {
            Socket socket = new(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                if (address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    try { socket.DualMode = true; } catch { }
                }

                socket.SendBufferSize = this.Options.BufferSize;
                socket.ReceiveBufferSize = this.Options.BufferSize;

                IPEndPoint remote = new(address, effectivePort);
                await socket.ConnectAsync(remote, connectCts.Token).ConfigureAwait(false);

                CancellationToken loopToken;

                lock (_sync)
                {
                    _socket = socket;
                    this.RemoteEndPoint = remote;
                    _host = effectiveHost;
                    _port = effectivePort;
                    _loopCts = new CancellationTokenSource();
                    loopToken = _loopCts.Token;
                    _ = Interlocked.Exchange(ref _connected, 1);
                }

                this.StartReceiveWorker(loopToken);

                bool isReconnect = Interlocked.Exchange(ref _hasEverConnected, 1) == 1;
                if (isReconnect)
                {
                    this.RaiseConnected();
                    this.RaiseReconnected(0);
                }
                else
                {
                    this.RaiseConnected();
                }

                _ = Interlocked.Exchange(ref _reconnecting, 0);
                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                try { socket.Dispose(); } catch { }
                _logger?.Warn($"[SDK.{nameof(UdpSession)}] Failed to connect to {address}:{effectivePort}: {ex.Message}", ex);
            }
        }

        _ = Interlocked.Exchange(ref _connected, 0);
        throw lastEx ?? new SocketException((int)SocketError.HostNotFound);
    }

    /// <summary>
    /// Connects to a <c>udp://host:port</c> endpoint.
    /// </summary>
    /// <param name="uri">The UDP endpoint URI.</param>
    /// <param name="ct">The cancellation token used to cancel the connect operation.</param>
    /// <returns>A task representing the asynchronous connect operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the URI scheme is not <c>udp</c>.</exception>
    public Task ConnectAsync(Uri uri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!string.Equals(uri.Scheme, "udp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"URI scheme must be 'udp', got '{uri.Scheme}'.", nameof(uri));
        }

        ushort port = uri.Port > 0 ? (ushort)uri.Port : this.Options.Port;
        return this.ConnectAsync(uri.Host, port, ct);
    }

    /// <inheritdoc/>
    public Task<bool> SendAsync(IPacket packet, CancellationToken ct = default)
        => this.SendAsync(packet, encrypt: false, ct);

    /// <summary>
    /// Serializes and sends a packet as a UDP datagram.
    /// </summary>
    /// <param name="packet">The packet to serialize and send.</param>
    /// <param name="encrypt">
    /// <see langword="true"/> to encrypt the outgoing packet payload before sending; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="ct">The cancellation token used to cancel the send operation.</param>
    /// <returns>
    /// <see langword="true"/> if the packet was sent successfully; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Compression and encryption follow the same packet-transform rules used by TCP sessions, but the final output
    /// is emitted as a single UDP datagram.
    /// </remarks>
    public async Task<bool> SendAsync(IPacket packet, bool encrypt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        if (!this.IsConnected)
        {
            throw new InvalidOperationException("Client not connected.");
        }

        if (packet.Length == 0)
        {
            return false;
        }

        BufferLease rawLease = BufferLease.Rent(packet.Length);
        int written = packet.Serialize(rawLease.SpanFull);
        rawLease.CommitLength(written);

        bool enableCompression = this.Options.EnableCompression && written >= this.Options.MinSizeToCompress;

        try
        {
            if (!enableCompression && !encrypt)
            {
                return await this.SendAsync(rawLease.Memory, ct).ConfigureAwait(false);
            }

            if (enableCompression && !encrypt)
            {
                int maxCompressedSize = FrameTransformer.GetMaxCompressedSize(written);
                BufferLease compressedLease = BufferLease.Rent(maxCompressedSize + FrameTransformer.Offset);

                try
                {
                    if (!FrameTransformer.TryCompress(rawLease, compressedLease))
                    {
                        return false;
                    }

                    compressedLease.Span.WriteFlagsLE(
                        compressedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));

                    return await this.SendAsync(compressedLease.Memory, ct).ConfigureAwait(false);
                }
                finally
                {
                    compressedLease.Dispose();
                }
            }

            if (!enableCompression && encrypt)
            {
                int maxCipherSize = FrameTransformer.GetMaxCiphertextSize(this.Options.Algorithm, rawLease.Length);
                BufferLease encryptedLease = BufferLease.Rent(maxCipherSize + FrameTransformer.Offset);

                try
                {
                    if (!FrameTransformer.TryEncrypt(rawLease, encryptedLease, this.Options.Secret, this.Options.Algorithm))
                    {
                        return false;
                    }

                    encryptedLease.Span.WriteFlagsLE(
                        encryptedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));

                    return await this.SendAsync(encryptedLease.Memory, ct).ConfigureAwait(false);
                }
                finally
                {
                    encryptedLease.Dispose();
                }
            }

            int maxCompressed = FrameTransformer.GetMaxCompressedSize(written);
            BufferLease compressLease = BufferLease.Rent(maxCompressed + FrameTransformer.Offset);

            try
            {
                if (!FrameTransformer.TryCompress(rawLease, compressLease))
                {
                    return false;
                }

                compressLease.Span.WriteFlagsLE(
                    compressLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));

                int maxCipher = FrameTransformer.GetMaxCiphertextSize(this.Options.Algorithm, compressLease.Length);
                BufferLease encryptLease = BufferLease.Rent(maxCipher + FrameTransformer.Offset);

                try
                {
                    if (!FrameTransformer.TryEncrypt(compressLease, encryptLease, this.Options.Secret, this.Options.Algorithm))
                    {
                        return false;
                    }

                    encryptLease.Span.WriteFlagsLE(
                        encryptLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));

                    return await this.SendAsync(encryptLease.Memory, ct).ConfigureAwait(false);
                }
                finally
                {
                    encryptLease.Dispose();
                }
            }
            finally
            {
                compressLease.Dispose();
            }
        }
        finally
        {
            rawLease.Dispose();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// When UDP authentication metadata is enabled and fully configured, the sent datagram format is
    /// <c>[payload][session-id][timestamp][nonce][poly1305-tag]</c>.
    /// </remarks>
    public async Task<bool> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(UdpSession));

        Socket socket = this.RequireConnectedSocket();

        if (payload.IsEmpty)
        {
            return false;
        }

        try
        {
            byte[] datagram = this.BuildDatagram(payload.Span);
            int sent = await socket.SendAsync(datagram, SocketFlags.None, ct).ConfigureAwait(false);

            if (sent != datagram.Length)
            {
                return false;
            }

            this.ReportBytesSent(sent);
            return true;
        }
        catch (Exception ex)
        {
            this.HandleTransportError(ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task DisconnectAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return Task.CompletedTask;
        }

        bool wasConnected = this.IsConnected;
        this.TearDownConnection();

        if (wasConnected)
        {
            this.RaiseDisconnected(new SocketException((int)SocketError.NotConnected));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        this.TearDownConnection();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        await this.DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    #endregion Public Methods

    #region Receive Pipeline

    private void StartReceiveWorker(CancellationToken loopToken) => _receiveTask = Task.Run(() => this.ReceiveLoopAsync(loopToken), CancellationToken.None);

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        Socket socket;

        try
        {
            socket = this.RequireConnectedSocket();
        }
        catch (Exception ex)
        {
            this.HandleTransportError(ex);
            return;
        }

        byte[] buffer = BufferLease.ByteArrayPool.Rent(this.Options.MaxPacketSize + AuthenticationMetadataSize);

        try
        {
            while (!token.IsCancellationRequested)
            {
                int received = await socket.ReceiveAsync(buffer, SocketFlags.None, token).ConfigureAwait(false);
                if (received <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                this.ReportBytesReceived(received);
                using BufferLease lease = BufferLease.CopyFrom(MemoryExtensions.AsSpan(buffer, 0, received));
                this.ProcessIncomingDatagram(lease);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            this.HandleTransportError(ex);
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);
        }
    }

    private void ProcessIncomingDatagram(BufferLease lease)
    {
        BufferLease current = lease;

        try
        {
            if (current.Length >= FrameTransformer.Offset)
            {
                PacketFlags flags = current.Span.ReadFlagsLE();

                if (flags.HasFlag(PacketFlags.ENCRYPTED))
                {
                    BufferLease decryptedLease = BufferLease.Rent(FrameTransformer.GetPlaintextLength(current.Span));

                    if (!FrameTransformer.TryDecrypt(current, decryptedLease, this.Options.Secret))
                    {
                        decryptedLease.Dispose();
                        return;
                    }

                    decryptedLease.Span.WriteFlagsLE(
                        decryptedLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.ENCRYPTED));

                    if (!ReferenceEquals(current, lease))
                    {
                        current.Dispose();
                    }

                    current = decryptedLease;
                    flags = current.Span.ReadFlagsLE();
                }

                if (flags.HasFlag(PacketFlags.COMPRESSED))
                {
                    BufferLease decompressedLease = BufferLease.Rent(FrameTransformer.GetDecompressedLength(current.Span));

                    if (!FrameTransformer.TryDecompress(current, decompressedLease))
                    {
                        decompressedLease.Dispose();
                        return;
                    }

                    decompressedLease.Span.WriteFlagsLE(
                        decompressedLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.COMPRESSED));

                    if (!ReferenceEquals(current, lease))
                    {
                        current.Dispose();
                    }

                    current = decompressedLease;
                }
            }

            this.DeliverMessage(current);
            current = null!;
        }
        finally
        {
            current?.Dispose();
        }
    }

    private void DeliverMessage(BufferLease lease)
    {
        ReadOnlyMemory<byte> asyncData = default;
        Delegate[]? handlers = OnMessageReceived?.GetInvocationList();
        Func<UdpSession, ReadOnlyMemory<byte>, Task>? asyncHandler = this.OnMessageReceivedAsync;

        if (asyncHandler is not null)
        {
            asyncData = lease.Span.ToArray();
        }

        try
        {
            if (handlers?.Length > 0)
            {
                foreach (Delegate handlerDelegate in handlers)
                {
                    BufferLease copy = BufferLease.CopyFrom(lease.Span);
                    EventHandler<IBufferLease> handler = (EventHandler<IBufferLease>)handlerDelegate;

                    _dispatcher.Post(() =>
                    {
                        try
                        {
                            handler(this, copy);
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"[SDK.{nameof(UdpSession)}] Sync handler faulted: {ex.Message}", ex);
                        }
                        finally
                        {
                            try { copy.Dispose(); } catch { }
                        }
                    });
                }
            }
        }
        finally
        {
            try { lease.Dispose(); } catch { }
        }

        if (asyncHandler is not null)
        {
            _dispatcher.Post(() => _ = this.InvokeAsyncHandler(asyncHandler, asyncData));
        }
    }

    private async Task InvokeAsyncHandler(
        Func<UdpSession, ReadOnlyMemory<byte>, Task> handler,
        ReadOnlyMemory<byte> data)
    {
        try
        {
            await handler(this, data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.Error($"[SDK.{nameof(UdpSession)}] Async handler faulted: {ex.Message}", ex);
        }
    }

    #endregion Receive Pipeline

    #region Datagram Construction

    /// <summary>
    /// Builds the final UDP datagram to send on the wire.
    /// </summary>
    /// <param name="payload">The payload bytes to send.</param>
    /// <returns>
    /// A byte array containing either the raw payload or the payload plus authenticated UDP metadata.
    /// </returns>
    private byte[] BuildDatagram(ReadOnlySpan<byte> payload)
    {
        if (!this.ShouldAppendAuthenticationMetadata())
        {
            return payload.ToArray();
        }

        IPEndPoint remote = (IPEndPoint)(this.RemoteEndPoint ?? throw new InvalidOperationException("Remote endpoint not available."));
        ISnowflake sessionId = this.SessionId ?? throw new InvalidOperationException("SessionId is required for authenticated UDP datagrams.");
        byte[] secret = this.Options.Secret;

        byte[] datagram = new byte[payload.Length + AuthenticationMetadataSize];
        payload.CopyTo(datagram);

        Span<byte> destination = datagram;
        Span<byte> idDestination = destination.Slice(payload.Length, Snowflake.Size);
        _ = sessionId.TryWriteBytes(idDestination);

        long timestamp = Clock.UnixMillisecondsNow();
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
            destination.Slice(payload.Length + Snowflake.Size, TimestampSize),
            timestamp);
        ulong nonce = Csprng.NextUInt64();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
            destination.Slice(payload.Length + Snowflake.Size + TimestampSize, NonceSize),
            nonce);

        Span<byte> remoteMeta = stackalloc byte[1 + 16 + sizeof(ushort)];
        int remoteMetaLength = EncodeRemoteEndpoint(remote, remoteMeta);

        Poly1305 poly = new(MemoryExtensions.AsSpan(secret, 0, Poly1305.KeySize));

        try
        {
            poly.Update(payload);
            poly.Update(idDestination);
            poly.Update(destination.Slice(payload.Length + Snowflake.Size, TimestampSize));
            poly.Update(destination.Slice(payload.Length + Snowflake.Size + TimestampSize, NonceSize));
            poly.Update(remoteMeta[..remoteMetaLength]);
            poly.FinalizeTag(destination.Slice(payload.Length + Snowflake.Size + TimestampSize + NonceSize, AuthenticationTagSize));
        }
        finally
        {
            poly.Clear();
        }

        return datagram;
    }

    /// <summary>
    /// Determines whether the current session state is sufficient to append authenticated UDP metadata.
    /// </summary>
    private bool ShouldAppendAuthenticationMetadata()
        => this.UseAuthenticationMetadata
        && this.SessionId is not null
        && this.Options.Secret is { Length: >= Poly1305.KeySize }
        && this.RemoteEndPoint is IPEndPoint;

    #endregion Datagram Construction

    #region Connection Lifecycle

    private void TearDownConnection()
    {
        _ = Interlocked.Exchange(ref _connected, 0);

        lock (_sync)
        {
            CancelAndDispose(ref _loopCts);

            try { _socket?.Close(); } catch { }
            try { _socket?.Dispose(); } catch { }

            _socket = null;
            this.RemoteEndPoint = null;
        }

        _receiveTask = null;
    }

    private Socket RequireConnectedSocket()
    {
        Socket? socket = _socket;
        return socket is not null && this.IsConnected
            ? socket
            : throw new InvalidOperationException("Client not connected.");
    }

    private void HandleTransportError(Exception ex)
    {
        this.RaiseError(ex);

        if (this.Options.ReconnectEnabled &&
            Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0)
        {
            _ = this.ReconnectLoopAsync(ex);
            return;
        }

        this.TearDownConnection();
        this.RaiseDisconnected(ex);
    }

    private async Task ReconnectLoopAsync(Exception cause)
    {
        _logger?.Warn($"[SDK.{nameof(UdpSession)}] Triggering reconnect after: {cause.Message}", cause);
        this.TearDownConnection();

        if (Volatile.Read(ref _disposed) == 1 || string.IsNullOrWhiteSpace(_host) || _port is null)
        {
            _ = Interlocked.Exchange(ref _reconnecting, 0);
            return;
        }

        int attempt = 0;
        long max = Math.Max(1, this.Options.ReconnectMaxDelayMillis);
        long delay = Math.Max(1, this.Options.ReconnectBaseDelayMillis);

        using CancellationTokenSource reconnectCts = new();

        while (Volatile.Read(ref _disposed) == 0 &&
               (this.Options.ReconnectMaxAttempts == 0 || attempt < this.Options.ReconnectMaxAttempts))
        {
            attempt++;
            long jitter = (long)(Csprng.NextDouble() * delay * 0.3);

            try
            {
                await Task.Delay((int)Math.Min(delay + jitter, int.MaxValue), reconnectCts.Token).ConfigureAwait(false);
                await this.ConnectAsync(_host, _port, reconnectCts.Token).ConfigureAwait(false);
                this.RaiseReconnected(attempt);
                _ = Interlocked.Exchange(ref _reconnecting, 0);
                return;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[SDK.{nameof(UdpSession)}] Reconnect attempt {attempt} failed: {ex.Message}", ex);
                delay = Math.Min(max, delay * 2);
            }
        }

        _ = Interlocked.Exchange(ref _reconnecting, 0);
        this.RaiseDisconnected(cause);
    }

    #endregion Connection Lifecycle

    #region Event Helpers

    private void ReportBytesSent(int count)
    {
        _ = Interlocked.Add(ref _bytesSent, count);
        try { OnBytesSent?.Invoke(this, count); } catch { }
    }

    private void ReportBytesReceived(int count)
    {
        _ = Interlocked.Add(ref _bytesReceived, count);
        try { OnBytesReceived?.Invoke(this, count); } catch { }
    }

    private void RaiseConnected()
    {
        try { OnConnected?.Invoke(this, EventArgs.Empty); } catch { }
    }

    private void RaiseDisconnected(Exception ex)
    {
        try { OnDisconnected?.Invoke(this, ex); } catch { }
    }

    private void RaiseError(Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
    }

    private void RaiseReconnected(int attempt)
    {
        try { OnReconnected?.Invoke(this, attempt); } catch { }
    }

    #endregion Event Helpers

    #region Static Helpers

    /// <summary>
    /// Cancels and disposes a cancellation token source, then clears the reference.
    /// </summary>
    private static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }
        cts = null;
    }

    // Encodes a remote endpoint into the compact metadata form used by UDP authentication.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EncodeRemoteEndpoint(IPEndPoint endpoint, Span<byte> destination)
    {
        byte[] addressBytes = endpoint.Address.GetAddressBytes();
        destination[0] = (byte)addressBytes.Length;
        MemoryExtensions.AsSpan(addressBytes).CopyTo(destination[1..]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
            destination.Slice(1 + addressBytes.Length, sizeof(ushort)),
            (ushort)endpoint.Port);

        return 1 + addressBytes.Length + sizeof(ushort);
    }

    #endregion Static Helpers
}
