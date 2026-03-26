// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Shared base class for TCP-style client sessions.
/// Contains common socket lifecycle, cleanup, send/receive glue, and event wiring.
/// Derived classes implement receive scheduling and framing construction.
/// </summary>
[DebuggerStepThrough]
[DebuggerDisplay("State = {State}, Connected = {IsConnected}")]
[EditorBrowsable(EditorBrowsableState.Never)]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public abstract class TcpSessionBase : IClientConnection, IAsyncDisposable
{
    #region Fields

    internal readonly Lock i_sync = new();

    internal FRAME_SENDER? i_sender;
    internal FRAME_READER? i_receiver;

    internal Socket? i_socket;
    internal Task? i_receiveTask;
    internal CancellationTokenSource? i_loopCts;

    internal int _disposed;


    private int _connectionState = (int)TcpSessionState.Disconnected;

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
    ITransportOptions IClientConnection.Options => this.Options;

    /// <summary>
    /// Gets the current lifecycle state of the session.
    /// </summary>
    public TcpSessionState State => (TcpSessionState)Volatile.Read(ref _connectionState);

    #endregion Properties

    #region Events

    /// <inheritdoc/>
    public event EventHandler? OnConnected;

    /// <inheritdoc/>
    public event EventHandler<Exception>? OnError;

    /// <inheritdoc/>
    public event EventHandler<long>? OnBytesSent;

    /// <inheritdoc/>
    public event EventHandler<long>? OnBytesReceived;

    /// <inheritdoc/>
    public event EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public event EventHandler<Exception>? OnDisconnected;

    /// <summary>
    /// Occurs when the session successfully reconnects after an unexpected disconnect.
    /// The event argument is the number of attempts it took.
    /// </summary>
    public event EventHandler<int>? OnReconnected;

    /// <inheritdoc/>
    public Func<TcpSessionBase, ReadOnlyMemory<byte>, Task>? OnMessageReceivedAsync { get; set; }

    #endregion Events

    #region Construction

    static TcpSessionBase() => Logging = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Constructs base session and loads TransportOptions from configuration.
    /// Derived classes are responsible for buffer configuration if needed.
    /// </summary>
    protected TcpSessionBase()
    {
        this.Options = null!;
        this.Catalog = null!;
    }

    #endregion Construction

    #region Public API

    /// <inheritdoc/>
    public abstract Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);

    /// <summary>
    /// Connects to the endpoint specified by <paramref name="uri"/>.
    /// Supports <c>tcp://host:port</c> scheme. Port defaults to <see cref="TransportOptions.Port"/> when absent.
    /// </summary>
    /// <param name="uri">The target URI. Must use the <c>tcp</c> scheme.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the URI scheme is not <c>tcp</c>.</exception>
    public Task ConnectAsync(Uri uri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"URI scheme must be 'tcp', got '{uri.Scheme}'.", nameof(uri));
        }

        ushort port = uri.Port > 0
            ? (ushort)uri.Port
            : this.Options.Port;

        return this.ConnectAsync(uri.Host, port, ct);
    }

    /// <inheritdoc/>
    public virtual bool IsConnected => i_socket?.Connected == true && Volatile.Read(ref _disposed) == 0;

    /// <inheritdoc/>
    public virtual Task<bool> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(TcpSessionBase));
        FRAME_SENDER? sender = Volatile.Read(ref i_sender);
        return sender is null ? throw new InvalidOperationException("Client not connected.") : sender.SendAsync(payload, ct);
    }

    /// <inheritdoc/>
    public virtual Task<bool> SendAsync(IPacket packet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(TcpSessionBase));
        FRAME_SENDER? sender = Volatile.Read(ref i_sender);
        return sender is null ? throw new InvalidOperationException("Client not connected.") : sender.SendAsync(packet, ct);
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
    /// Token used to abort the send operation. Defaults to <see cref="CancellationToken.None"/>.
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
    public async Task<bool> SendAsync(
        IPacket packet,
        bool encrypt,
        CancellationToken cancellationToken = default)
    {
        // Serialize into a pooled buffer — avoids allocating a byte[] per send.
        BufferLease rawLease = BufferLease.Rent(packet.Length);
        int written = packet.Serialize(rawLease.SpanFull);
        rawLease.CommitLength(written);

        bool enableCompress = this.Options.EnableCompression && written >= this.Options.MinSizeToCompress;

        try
        {
            // ----------------------------------------------------------------
            // Case 1: plain — no compression, no encryption
            // ----------------------------------------------------------------
            if (!enableCompress && !encrypt)
            {
                Logging?.Trace($"[SDK.{this.GetType().Name}] SendAsync: Send plain payload, size={written}");
                return await this.SendAsync(rawLease.Memory, cancellationToken).ConfigureAwait(false);
            }

            // ----------------------------------------------------------------
            // Case 2: compress only
            // ----------------------------------------------------------------
            if (enableCompress && !encrypt)
            {
                int maxCompressedSize = FrameTransformer.GetMaxCompressedSize(written);
                BufferLease compressedLease = BufferLease.Rent(maxCompressedSize + FrameTransformer.Offset);
                try
                {
                    if (!FrameTransformer.TryCompress(rawLease, compressedLease))
                    {
                        Logging?.Warn($"[SDK.{this.GetType().Name}] SendAsync: Compression failed, packet={packet.GetType().Name}, size={written}");
                        return false;
                    }

                    Logging?.Trace($"[SDK.{this.GetType().Name}] SendAsync: Compressed and sent, original={written}, compressed={compressedLease.Length}");
                    compressedLease.Span.WriteFlagsLE(
                        compressedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.COMPRESSED));

                    return await this.SendAsync(compressedLease.Memory, cancellationToken).ConfigureAwait(false);
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
                int maxCipherSize = FrameTransformer.GetMaxCiphertextSize(this.Options.Algorithm, rawLease.Length);
                BufferLease encryptedLease = BufferLease.Rent(maxCipherSize + FrameTransformer.Offset);
                try
                {
                    if (!FrameTransformer.TryEncrypt(rawLease, encryptedLease, this.Options.Secret, this.Options.Algorithm))
                    {
                        Logging?.Error($"[SDK.{this.GetType().Name}:{nameof(SendAsync)}] encrypt-failed");
                        return false;
                    }

                    encryptedLease.Span.WriteFlagsLE(
                        encryptedLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));

                    Logging?.Trace($"[SDK.{this.GetType().Name}] SendAsync: Encrypted and sent, len={encryptedLease.Length}");
                    return await this.SendAsync(encryptedLease.Memory, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    encryptedLease.Dispose();
                }
            }

            // ----------------------------------------------------------------
            // Case 4: compress then encrypt
            // ----------------------------------------------------------------
            int maxCompressed = FrameTransformer.GetMaxCompressedSize(written);
            BufferLease compressLease = BufferLease.Rent(maxCompressed + FrameTransformer.Offset);
            try
            {
                if (!FrameTransformer.TryCompress(rawLease, compressLease))
                {
                    Logging?.Warn($"[SDK.{this.GetType().Name}] SendAsync: Compress-then-encrypt compression failed, len={written}");
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
                        Logging?.Error($"[SDK.{this.GetType().Name}:{nameof(SendAsync)}] encrypt-after-compress-failed");
                        return false;
                    }

                    encryptLease.Span.WriteFlagsLE(
                        encryptLease.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));

                    Logging?.Trace($"[SDK.{this.GetType().Name}] SendAsync: Compress+Encrypt, final-size={encryptLease.Length}");
                    return await this.SendAsync(encryptLease.Memory, cancellationToken).ConfigureAwait(false);
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
        catch (OperationCanceledException)
        {
            Logging?.Info($"[SDK.{this.GetType().Name}] SendAsync: Operation canceled");
            throw;
        }
        catch (Exception ex)
        {
            Logging?.Error($"[SDK.{this.GetType().Name}:{nameof(SendAsync)}] send-failed", ex);
            return false;
        }
        finally
        {
            rawLease.Dispose();
        }
    }

    /// <inheritdoc/>
    public virtual Task DisconnectAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            Logging?.Info($"[SDK.{this.GetType().Name}] Disconnect called, but already disposed.");
            return Task.CompletedTask;
        }

        this.TearDownConnection();
        Logging?.Info($"[SDK.{this.GetType().Name}] Disconnected (requested).");
        try { OnDisconnected?.Invoke(this, null!); } catch (Exception ex) { Logging?.Error($"[SDK.{this.GetType().Name}] OnDisconnected handler threw: {ex.Message}", ex); }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            Logging?.Info($"[SDK.{this.GetType().Name}] Dispose called but was already disposed.");
            return;
        }

        this.SetState(TcpSessionState.Disposed);
        this.TearDownConnection();
        Logging?.Info($"[SDK.{this.GetType().Name}] Disposed.");
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disconnects and releases all resources.
    /// Prefer <c>await using</c> over <c>using</c> when calling from async code.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        this.SetState(TcpSessionState.Disposed);
        await this.DisconnectAsync().ConfigureAwait(false);
        Logging?.Info($"[SDK.{this.GetType().Name}] DisposeAsync completed.");
        GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Protected Methods

    /// <inheritdoc/>
    protected void RaiseConnected()
    {
        this.SetState(TcpSessionState.Connected);
        Logging?.Info($"[SDK.{this.GetType().Name}] Connected.");
        try
        {
            OnConnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logging?.Error($"[SDK.{this.GetType().Name}] OnConnected handler threw: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    protected void RaiseDisconnected(Exception? ex)
    {
        this.SetState(TcpSessionState.Disconnected);
        Logging?.Info($"[SDK.{this.GetType().Name}] Disconnected.");
        try
        {
            OnDisconnected?.Invoke(this, ex!);
        }
        catch (Exception handlerEx)
        {
            Logging?.Error($"[SDK.{this.GetType().Name}] OnDisconnected handler threw: {handlerEx.Message}", handlerEx);
        }
    }

    /// <inheritdoc/>
    protected void RaiseError(Exception ex)
    {
        Logging?.Info($"[SDK.{this.GetType().Name}] RaiseError event fired: {ex.Message}");
        try
        {
            OnError?.Invoke(this, ex);
        }
        catch (Exception handlerEx)
        {
            Logging?.Error($"[SDK.{this.GetType().Name}] RaiseError handler threw: {handlerEx.Message}", handlerEx);
        }
    }

    /// <inheritdoc/>
    protected void RaiseBytesSent(long bytes)
    {
        Logging?.Trace($"[SDK.{this.GetType().Name}] BytesSent={bytes}");
        try
        {
            OnBytesSent?.Invoke(this, bytes);
        }
        catch (Exception ex)
        {
            Logging?.Error($"[SDK.{this.GetType().Name}] BytesSent handler threw: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    protected void RaiseBytesReceived(long bytes)
    {
        Logging?.Trace($"[SDK.{this.GetType().Name}] BytesReceived={bytes}");
        try
        {
            OnBytesReceived?.Invoke(this, bytes);
        }
        catch (Exception ex)
        {
            Logging?.Error($"[SDK.{this.GetType().Name}] BytesReceived handler threw: {ex.Message}", ex);
        }
    }


    /// <summary>
    /// Protected helper for subclasses to set up frame helpers (_sender/_receiver).
    /// Subclasses should instantiate _sender/_receiver and bind desired error/byte-report callbacks.
    /// </summary>
    [MemberNotNull(nameof(i_sender), nameof(i_receiver))]
    protected abstract void InitializeFrame();

    /// <summary>
    /// Start/stage receive worker - scheduling differs per subclass (TaskManager vs Task.Run).
    /// </summary>
    /// <param name="loopToken"></param>
    protected abstract void StartReceiveWorker(CancellationToken loopToken);

    /// <summary>
    /// Common cleanup logic: cancel background loops, dispose sender, close socket, null out reader.
    /// Subclasses may override to perform additional cleanup (e.g., cancel TaskManager handles).
    /// </summary>
    protected virtual void TearDownConnection()
    {
        this.SetState(TcpSessionState.Disconnected);
        lock (i_sync)
        {
            if (i_loopCts is not null)
            {
                CancelAndDispose(ref i_loopCts);

                Logging?.Debug($"[SDK.{this.GetType().Name}] TearDownConnection: Loop token cancelled and disposed");
            }

            try
            {
                FRAME_SENDER? prevSender = Interlocked.Exchange(ref i_sender, null);
                prevSender?.Dispose();
            }
            catch (Exception ex)
            {
                Logging?.Warn($"[SDK.{this.GetType().Name}] TearDownConnection: Sender cleanup threw: {ex.Message}", ex);
            }

            try { i_socket?.Shutdown(SocketShutdown.Both); } catch { }
            try { i_socket?.Close(); i_socket?.Dispose(); } catch { }

            Logging?.Debug($"[SDK.{this.GetType().Name}] TearDownConnection: Socket closed and disposed.");

            i_socket = null;
            i_receiver = null;
        }

        i_receiveTask = null;
    }

    /// <summary>
    /// Cancel and dispose a CancellationTokenSource under lock.
    /// </summary>
    /// <param name="cts"></param>
    protected static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }
        cts = null;
    }

    /// <summary>
    /// Throw if socket is not connected; used by FRAME helpers.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    protected Socket RequireConnectedSocket()
    {
        Socket? s = i_socket;
        return s?.Connected == true
            ? s
            : throw new InvalidOperationException("Client not connected.");
    }

    /// <summary>
    /// Report byte counts to subscribers. Subclasses can override to include counters.
    /// </summary>
    /// <param name="count"></param>
    protected virtual void ReportBytesSent(int count)
    {
        try { OnBytesSent?.Invoke(this, count); } catch { }
    }

    /// <summary>
    /// Report byte counts to subscribers. Subclasses can override to include counters.
    /// </summary>
    /// <param name="count"></param>
    protected virtual void ReportBytesReceived(int count)
    {
        try { OnBytesReceived?.Invoke(this, count); } catch { }
    }

    /// <summary>
    /// Default receive message delivery for synchronous subscribers.
    /// Derived classes that need async handler support should override.
    /// </summary>
    /// <param name="lease"></param>
    protected virtual void HandleReceiveMessage(BufferLease lease)
    {
        ReadOnlyMemory<byte> asyncData = default;
        Delegate[]? handlers = OnMessageReceived?.GetInvocationList();
        Func<TcpSessionBase, ReadOnlyMemory<byte>, Task>? asyncHandler = this.OnMessageReceivedAsync;

        if (asyncHandler is not null)
        {
            asyncData = lease.Span.ToArray();
        }

        try
        {
            if (handlers?.Length > 0)
            {
                foreach (Delegate d in handlers)
                {
                    BufferLease copy = BufferLease.CopyFrom(lease.Span);
                    EventHandler<IBufferLease> handler = (EventHandler<IBufferLease>)d;

                    Dispatcher.Post(() =>
                    {
                        try
                        {
                            Logging?.Debug($"[SDK.{nameof(TcpSessionBase)}] Dispatch sync handler, length={copy.Length}");
                            handler.Invoke(this, copy);
                        }
                        catch (Exception ex)
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
                _ = this.InvokeAsyncHandler(asyncHandler, asyncData);
            });
        }
    }

    /// <summary>
    /// Default send error handler: notify subscribers and tear down connection.
    /// Subclasses can override to implement reconnect semantics.
    /// </summary>
    /// <param name="ex"></param>
    protected virtual void HandleSendError(Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        this.TearDownConnection();
    }

    /// <summary>
    /// Default receive error handler: notify subscribers and tear down connection.
    /// Subclasses can override to implement reconnect semantics.
    /// </summary>
    /// <param name="ex"></param>
    protected virtual void HandleReceiveError(Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        this.TearDownConnection();
    }

    /// <summary>
    /// Transitions the session to <paramref name="next"/> and logs the change.
    /// Thread-safe via atomic write.
    /// </summary>
    /// <param name="next"></param>
    protected void SetState(TcpSessionState next)
    {
        TcpSessionState prev = (TcpSessionState)Interlocked.Exchange(
            ref _connectionState, (int)next);

        if (prev != next)
        {
            Logging?.Debug($"[SDK.{this.GetType().Name}] State: {prev} → {next}");
        }
    }

    /// <summary>
    /// Fires <see cref="OnReconnected"/> with the attempt count.
    /// </summary>
    /// <param name="attempt"></param>
    protected void RaiseReconnected(int attempt)
    {
        Logging?.Info($"[SDK.{this.GetType().Name}] Reconnected (attempt {attempt}).");
        try
        {
            OnReconnected?.Invoke(this, attempt);
        }
        catch (Exception ex)
        {
            Logging?.Error($"[SDK.{this.GetType().Name}] OnReconnected handler threw: {ex.Message}", ex);
        }
    }

    #endregion Protected Methods

    #region Private Methods

    private async Task InvokeAsyncHandler(
        Func<TcpSessionBase, ReadOnlyMemory<byte>, Task> handler,
        ReadOnlyMemory<byte> data)
    {
        try
        {
            await handler(this, data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging?.Error($"[SDK.{nameof(TcpSessionBase)}] async handler faulted: {ex.Message}", ex);
        }
    }

    #endregion Private Methods
}
