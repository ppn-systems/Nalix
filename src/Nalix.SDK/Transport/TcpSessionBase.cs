// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Transport;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport;

/// <summary>
/// Shared base class for TCP-style client sessions.
/// Contains common socket lifecycle, cleanup, send/receive glue, and event wiring.
/// Derived classes implement receive scheduling and framing construction.
/// </summary>
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerDisplay("State = {State}, Connected = {IsConnected}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public abstract class TcpSessionBase : IClientConnection, System.IAsyncDisposable
{
    #region Fields

    internal readonly System.Threading.Lock i_sync = new();

    internal FRAME_SENDER? i_sender;
    internal FRAME_READER? i_receiver;

    internal System.Net.Sockets.Socket? i_socket;
    internal System.Threading.Tasks.Task? i_receiveTask;
    internal System.Threading.CancellationTokenSource? i_loopCts;

    internal System.Int32 _disposed = 0;


    private System.Int32 _connectionState = (System.Int32)TcpSessionState.Disconnected;

    /// <inheritdoc/>
    internal static readonly ILogger? Logging;

    /// <inheritdoc/>
    internal static readonly IThreadDispatcher Dispatcher = InstanceManager.Instance.GetExistingInstance<IThreadDispatcher>() ?? new InlineDispatcher();

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    public TransportOptions Options { get; protected set; }

    /// <inheritdoc/>
    public IPacketRegistry Catalog { get; protected set; }

    /// <inheritdoc/>
    ITransportOptions IClientConnection.Options => Options;

    /// <summary>
    /// Gets the current lifecycle state of the session.
    /// </summary>
    public TcpSessionState State => (TcpSessionState)System.Threading.Volatile.Read(ref _connectionState);

    #endregion Properties

    #region Events

    /// <inheritdoc/>
    public event System.EventHandler? OnConnected;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception>? OnError;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64>? OnBytesSent;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64>? OnBytesReceived;

    /// <inheritdoc/>
    public event System.EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception>? OnDisconnected;

    /// <summary>
    /// Occurs when the session successfully reconnects after an unexpected disconnect.
    /// The event argument is the number of attempts it took.
    /// </summary>
    public event System.EventHandler<System.Int32>? OnReconnected;

    /// <inheritdoc/>
    public System.Func<TcpSessionBase, System.ReadOnlyMemory<System.Byte>, System.Threading.Tasks.Task>? OnMessageReceivedAsync;

    #endregion

    #region Construction

    static TcpSessionBase() => Logging = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Constructs base session and loads TransportOptions from configuration.
    /// Derived classes are responsible for buffer configuration if needed.
    /// </summary>
    protected TcpSessionBase()
    {
        Options = null!;
        Catalog = null!;
    }

    #endregion Construction

    #region Public API

    /// <inheritdoc/>
    public abstract System.Threading.Tasks.Task ConnectAsync(System.String? host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default);

    /// <summary>
    /// Connects to the endpoint specified by <paramref name="uri"/>.
    /// Supports <c>tcp://host:port</c> scheme. Port defaults to <see cref="TransportOptions.Port"/> when absent.
    /// </summary>
    /// <param name="uri">The target URI. Must use the <c>tcp</c> scheme.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when the URI scheme is not <c>tcp</c>.</exception>
    public System.Threading.Tasks.Task ConnectAsync(System.Uri uri, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(uri);

        if (!System.String.Equals(uri.Scheme, "tcp", System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.ArgumentException(
                $"URI scheme must be 'tcp', got '{uri.Scheme}'.", nameof(uri));
        }

        System.UInt16 port = uri.Port > 0
            ? (System.UInt16)uri.Port
            : Options.Port;

        return ConnectAsync(uri.Host, port, ct);
    }

    /// <inheritdoc/>
    public virtual System.Boolean IsConnected => i_socket?.Connected == true && System.Threading.Volatile.Read(ref _disposed) == 0;

    /// <inheritdoc/>
    public virtual System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpSessionBase));
        var sender = System.Threading.Volatile.Read(ref i_sender);
        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(payload, ct);
    }

    /// <inheritdoc/>
    public virtual System.Threading.Tasks.Task<System.Boolean> SendAsync(IPacket packet, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpSessionBase));
        var sender = System.Threading.Volatile.Read(ref i_sender);
        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(packet, ct);
    }

    /// <summary>
    /// Serializes <paramref name="packet"/> and transmits it over the TCP connection,
    /// applying compression and/or encryption according to the current session state
    /// and the <paramref name="encrypt"/> flag.
    /// </summary>
    /// <param name="packet">
    /// The packet to serialize and send. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="encrypt">
    /// <see langword="true"/> to encrypt the outbound frame using the session's
    /// negotiated algorithm and secret key; <see langword="false"/> to send plaintext.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to abort the send operation. Defaults to <see cref="System.Threading.CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the frame was successfully sent;
    /// <see langword="false"/> if compression or encryption failed before the
    /// network write was attempted.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Serialization always writes into a pooled <see cref="BufferLease"/> to avoid
    /// heap allocations on the hot send path. The lease is always disposed before this
    /// method returns, regardless of the outcome.
    /// </para>
    /// <para>
    /// Four paths are taken depending on session state:
    /// <list type="number">
    ///   <item>Plain — no compression, no encryption. The raw serialized bytes are sent directly.</item>
    ///   <item>Compress only — raw bytes are compressed; the <c>COMPRESSED</c> flag is written into the frame header.</item>
    ///   <item>Encrypt only — raw bytes are encrypted; the <c>ENCRYPTED</c> flag is written into the frame header.</item>
    ///   <item>Compress then encrypt — compression is applied first, then encryption;
    ///         both the <c>COMPRESSED</c> and <c>ENCRYPTED</c> flags are written.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Compression is only attempted when <c>CompressionOptions.Enabled</c> is
    /// <see langword="true"/> and the serialized payload size meets or exceeds
    /// <c>CompressionOptions.MinSizeToCompress</c>. Encrypting a small packet that
    /// was not worth compressing uses path 3.
    /// </para>
    /// </remarks>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        IPacket packet,
        System.Boolean encrypt,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // Serialize into a pooled buffer — avoids allocating a byte[] per send.
        BufferLease rawLease = BufferLease.Rent(packet.Length);
        System.Int32 written = packet.Serialize(rawLease.SpanFull);
        rawLease.CommitLength(written);

        System.Boolean enableCompress = Options.EnableCompression && written >= Options.MinSizeToCompress;

        try
        {
            // ----------------------------------------------------------------
            // Case 1: plain — no compression, no encryption
            // ----------------------------------------------------------------
            if (!enableCompress && !encrypt)
            {
                Logging?.Trace($"[SDK.{GetType().Name}] SendAsync: Send plain payload, size={written}");
                return await SendAsync(rawLease.Memory, cancellationToken).ConfigureAwait(false);
            }

            // ----------------------------------------------------------------
            // Case 2: compress only
            // ----------------------------------------------------------------
            if (enableCompress && !encrypt)
            {
                System.Int32 maxCompressedSize = FrameTransformer.GetMaxCompressedSize(written);
                BufferLease compressedLease = BufferLease.Rent(maxCompressedSize + FrameTransformer.Offset);
                try
                {
                    if (!FrameTransformer.TryCompress(rawLease, compressedLease))
                    {
                        Logging?.Warn($"[SDK.{GetType().Name}] SendAsync: Compression failed, packet={packet.GetType().Name}, size={written}");
                        return false;
                    }

                    Logging?.Trace($"[SDK.{GetType().Name}] SendAsync: Compressed and sent, original={written}, compressed={compressedLease.Length}");
                    compressedLease.Span.WriteFlagsLE(
                        compressedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));

                    return await SendAsync(compressedLease.Memory, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    compressedLease.Dispose();
                }
            }

            // ----------------------------------------------------------------
            // Case 3: encrypt only
            // ----------------------------------------------------------------
            if (!enableCompress && encrypt)
            {
                System.Int32 maxCipherSize = FrameTransformer.GetMaxCiphertextSize(Options.Algorithm, rawLease.Length);
                BufferLease encryptedLease = BufferLease.Rent(maxCipherSize + FrameTransformer.Offset);
                try
                {
                    if (!FrameTransformer.TryEncrypt(rawLease, encryptedLease, Options.Secret, Options.Algorithm))
                    {
                        Logging?.Error($"[SDK.{GetType().Name}:{nameof(SendAsync)}] encrypt-failed");
                        return false;
                    }

                    encryptedLease.Span.WriteFlagsLE(
                        encryptedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));

                    Logging?.Trace($"[SDK.{GetType().Name}] SendAsync: Encrypted and sent, len={encryptedLease.Length}");
                    return await SendAsync(encryptedLease.Memory, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    encryptedLease.Dispose();
                }
            }

            // ----------------------------------------------------------------
            // Case 4: compress then encrypt
            // ----------------------------------------------------------------
            System.Int32 maxCompressed = FrameTransformer.GetMaxCompressedSize(written);
            BufferLease compressLease = BufferLease.Rent(maxCompressed + FrameTransformer.Offset);
            try
            {
                if (!FrameTransformer.TryCompress(rawLease, compressLease))
                {
                    Logging?.Warn($"[SDK.{GetType().Name}] SendAsync: Compress-then-encrypt compression failed, len={written}");
                    return false;
                }

                compressLease.Span.WriteFlagsLE(
                    compressLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));

                System.Int32 maxCipher = FrameTransformer.GetMaxCiphertextSize(Options.Algorithm, compressLease.Length);
                BufferLease encryptLease = BufferLease.Rent(maxCipher + FrameTransformer.Offset);
                try
                {
                    if (!FrameTransformer.TryEncrypt(compressLease, encryptLease, Options.Secret, Options.Algorithm))
                    {
                        Logging?.Error($"[SDK.{GetType().Name}:{nameof(SendAsync)}] encrypt-after-compress-failed");
                        return false;
                    }

                    encryptLease.Span.WriteFlagsLE(
                        encryptLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));

                    Logging?.Trace($"[SDK.{GetType().Name}] SendAsync: Compress+Encrypt, final-size={encryptLease.Length}");
                    return await SendAsync(encryptLease.Memory, cancellationToken).ConfigureAwait(false);
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
        catch (System.OperationCanceledException)
        {
            Logging?.Info($"[SDK.{GetType().Name}] SendAsync: Operation canceled");
            throw;
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{GetType().Name}:{nameof(SendAsync)}] send-failed", ex);
            return false;
        }
        finally
        {
            rawLease.Dispose();
        }
    }

    /// <inheritdoc/>
    public virtual System.Threading.Tasks.Task DisconnectAsync()
    {
        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            Logging?.Info($"[SDK.{GetType().Name}] Disconnect called, but already disposed.");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        TearDownConnection();
        Logging?.Info($"[SDK.{GetType().Name}] Disconnected (requested).");
        try { OnDisconnected?.Invoke(this, null!); } catch (System.Exception ex) { Logging?.Error($"[SDK.{GetType().Name}] OnDisconnected handler threw: {ex.Message}", ex); }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            Logging?.Info($"[SDK.{GetType().Name}] Dispose called but was already disposed.");
            return;
        }

        SetState(TcpSessionState.Disposed);
        TearDownConnection();
        Logging?.Info($"[SDK.{GetType().Name}] Disposed.");
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disconnects and releases all resources.
    /// Prefer <c>await using</c> over <c>using</c> when calling from async code.
    /// </summary>
    public virtual async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        SetState(TcpSessionState.Disposed);
        await DisconnectAsync().ConfigureAwait(false);
        Logging?.Info($"[SDK.{GetType().Name}] DisposeAsync completed.");
        System.GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Protected Methods

    /// <inheritdoc/>
    protected void RaiseConnected()
    {
        SetState(TcpSessionState.Connected);
        Logging?.Info($"[SDK.{GetType().Name}] Connected.");
        try
        {
            OnConnected?.Invoke(this, System.EventArgs.Empty);
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{GetType().Name}] OnConnected handler threw: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    protected void RaiseDisconnected(System.Exception? ex)
    {
        SetState(TcpSessionState.Disconnected);
        Logging?.Info($"[SDK.{GetType().Name}] Disconnected.");
        try
        {
            OnDisconnected?.Invoke(this, ex!);
        }
        catch (System.Exception handlerEx)
        {
            Logging?.Error($"[SDK.{GetType().Name}] OnDisconnected handler threw: {handlerEx.Message}", handlerEx);
        }
    }

    /// <inheritdoc/>
    protected void RaiseError(System.Exception ex)
    {
        Logging?.Info($"[SDK.{GetType().Name}] RaiseError event fired: {ex.Message}");
        try
        {
            OnError?.Invoke(this, ex);
        }
        catch (System.Exception handlerEx)
        {
            Logging?.Error($"[SDK.{GetType().Name}] RaiseError handler threw: {handlerEx.Message}", handlerEx);
        }
    }

    /// <inheritdoc/>
    protected void RaiseBytesSent(System.Int64 bytes)
    {
        Logging?.Trace($"[SDK.{GetType().Name}] BytesSent={bytes}");
        try
        {
            OnBytesSent?.Invoke(this, bytes);
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{GetType().Name}] BytesSent handler threw: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    protected void RaiseBytesReceived(System.Int64 bytes)
    {
        Logging?.Trace($"[SDK.{GetType().Name}] BytesReceived={bytes}");
        try
        {
            OnBytesReceived?.Invoke(this, bytes);
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{GetType().Name}] BytesReceived handler threw: {ex.Message}", ex);
        }
    }


    /// <summary>
    /// Protected helper for subclasses to set up frame helpers (_sender/_receiver).
    /// Subclasses should instantiate _sender/_receiver and bind desired error/byte-report callbacks.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(i_sender), nameof(i_receiver))]
    protected abstract void InitializeFrame();

    /// <summary>
    /// Start/stage receive worker - scheduling differs per subclass (TaskManager vs Task.Run).
    /// </summary>
    protected abstract void StartReceiveWorker(System.Threading.CancellationToken loopToken);

    /// <summary>
    /// Common cleanup logic: cancel background loops, dispose sender, close socket, null out reader.
    /// Subclasses may override to perform additional cleanup (e.g., cancel TaskManager handles).
    /// </summary>
    protected virtual void TearDownConnection()
    {
        SetState(TcpSessionState.Disconnected);
        lock (i_sync)
        {
            if (i_loopCts is not null)
            {
                CancelAndDispose(ref i_loopCts);

                Logging?.Debug($"[SDK.{GetType().Name}] TearDownConnection: Loop token cancelled and disposed");
            }

            try
            {
                var prevSender = System.Threading.Interlocked.Exchange(ref i_sender, null);
                prevSender?.Dispose();
            }
            catch (System.Exception ex)
            {
                Logging?.Warn($"[SDK.{GetType().Name}] TearDownConnection: Sender cleanup threw: {ex.Message}", ex);
            }

            try { i_socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { i_socket?.Close(); i_socket?.Dispose(); } catch { }

            Logging?.Debug($"[SDK.{GetType().Name}] TearDownConnection: Socket closed and disposed.");

            i_socket = null!;
            i_receiver = null!;
        }

        i_receiveTask = null!;
    }

    /// <summary>
    /// Cancel and dispose a CancellationTokenSource under lock.
    /// </summary>
    protected static void CancelAndDispose(ref System.Threading.CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }
        cts = null!;
    }

    /// <summary>
    /// Throw if socket is not connected; used by FRAME helpers.
    /// </summary>
    protected System.Net.Sockets.Socket RequireConnectedSocket()
    {
        System.Net.Sockets.Socket? s = i_socket;
        return s?.Connected == true
            ? s
            : throw new System.InvalidOperationException("Client not connected.");
    }

    /// <summary>
    /// Report byte counts to subscribers. Subclasses can override to include counters.
    /// </summary>
    protected virtual void ReportBytesSent(System.Int32 count)
    {
        try { OnBytesSent?.Invoke(this, count); } catch { }
    }

    /// <summary>
    /// Report byte counts to subscribers. Subclasses can override to include counters.
    /// </summary>
    protected virtual void ReportBytesReceived(System.Int32 count)
    {
        try { OnBytesReceived?.Invoke(this, count); } catch { }
    }

    /// <summary>
    /// Default receive message delivery for synchronous subscribers.
    /// Derived classes that need async handler support should override.
    /// </summary>
    protected virtual void HandleReceiveMessage(BufferLease lease)
    {
        System.ReadOnlyMemory<System.Byte> asyncData = default;
        System.Delegate[]? handlers = OnMessageReceived?.GetInvocationList();
        System.Func<TcpSessionBase, System.ReadOnlyMemory<System.Byte>, System.Threading.Tasks.Task>? asyncHandler = OnMessageReceivedAsync;

        if (asyncHandler is not null)
        {
            asyncData = lease.Span.ToArray();
        }

        try
        {
            if (handlers?.Length > 0)
            {
                foreach (System.Delegate d in handlers)
                {
                    BufferLease copy = BufferLease.CopyFrom(lease.Span);
                    System.EventHandler<IBufferLease> handler = (System.EventHandler<IBufferLease>)d;

                    Dispatcher.Post(() =>
                    {
                        try
                        {
                            Logging?.Debug($"[SDK.{nameof(TcpSessionBase)}] Dispatch sync handler, length={copy.Length}");
                            handler.Invoke(this, copy);
                        }
                        catch (System.Exception ex)
                        {
                            Logging?.Error($"[SDK.{nameof(TcpSessionBase)}] sync handler faulted: {ex.Message}", ex);
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
            Dispatcher.Post(() =>
            {
                Logging?.Debug($"[SDK.{nameof(TcpSessionBase)}] Dispatch async handler, length={asyncData.Length}");
                _ = InvokeAsyncHandler(asyncHandler, asyncData);
            });
        }
    }

    /// <summary>
    /// Default send error handler: notify subscribers and tear down connection.
    /// Subclasses can override to implement reconnect semantics.
    /// </summary>
    protected virtual void HandleSendError(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        TearDownConnection();
    }

    /// <summary>
    /// Default receive error handler: notify subscribers and tear down connection.
    /// Subclasses can override to implement reconnect semantics.
    /// </summary>
    protected virtual void HandleReceiveError(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        TearDownConnection();
    }

    /// <summary>
    /// Transitions the session to <paramref name="next"/> and logs the change.
    /// Thread-safe via atomic write.
    /// </summary>
    protected void SetState(TcpSessionState next)
    {
        TcpSessionState prev = (TcpSessionState)System.Threading.Interlocked.Exchange(
            ref _connectionState, (System.Int32)next);

        if (prev != next)
        {
            Logging?.Debug($"[SDK.{GetType().Name}] State: {prev} → {next}");
        }
    }

    /// <summary>
    /// Fires <see cref="OnReconnected"/> with the attempt count.
    /// </summary>
    protected void RaiseReconnected(System.Int32 attempt)
    {
        Logging?.Info($"[SDK.{GetType().Name}] Reconnected (attempt {attempt}).");
        try
        {
            OnReconnected?.Invoke(this, attempt);
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{GetType().Name}] OnReconnected handler threw: {ex.Message}", ex);
        }
    }

    #endregion Protected Methods

    #region Private Methods

    private async System.Threading.Tasks.Task InvokeAsyncHandler(
        System.Func<TcpSessionBase, System.ReadOnlyMemory<System.Byte>, System.Threading.Tasks.Task> handler,
        System.ReadOnlyMemory<System.Byte> data)
    {
        try
        {
            await handler(this, data).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{nameof(TcpSessionBase)}] async handler faulted: {ex.Message}", ex);
        }
    }

    #endregion Private Methods
}