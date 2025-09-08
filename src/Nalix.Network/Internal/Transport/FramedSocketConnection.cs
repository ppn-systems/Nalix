// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooled;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Objects;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

#nullable enable

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Manages the socket connection and handles sending/receiving data with caching and logging.
/// The receive path uses <see cref="PooledSocketReceiveContext"/> (SAEA-backed, pooled via
/// <see cref="ObjectPoolManager"/>) to eliminate per-receive allocations and scale
/// stably at 10 000+ concurrent connections.
///
/// <para><b>DDoS Protection (Layer 1 — Per-Connection Throttle):</b><br/>
/// Each connection tracks how many packets are currently pending processing via
/// <c>_pendingProcessCallbacks</c>. If a single connection floods packets faster
/// than the handler can process them, incoming packets are dropped at the receive
/// loop level — before they ever reach <see cref="AsyncCallback"/> or the ThreadPool.
/// This prevents a single abusive IP from consuming the global callback quota and
/// starving legitimate connections.</para>
/// </summary>
/// <param name="socket">The accepted, connected socket.</param>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{ToString()}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class FramedSocketConnection(System.Net.Sockets.Socket socket) : System.IDisposable
{
    #region Const

    private const System.Byte HeaderSize = sizeof(System.UInt16);

    /// <summary>
    /// Maximum number of packets that may be queued-but-not-yet-processed
    /// for a single connection at any moment.
    /// <para>
    /// When a connection sends packets faster than <see cref="AsyncCallback"/>
    /// can dispatch them, excess packets are dropped and a warning is logged.
    /// Legitimate clients rarely queue more than 1–2 packets simultaneously;
    /// a value of 8 gives generous headroom while blocking flood attacks.
    /// </para>
    /// </summary>
    private const System.Int32 MaxPerConnectionPendingPackets = 8;

    #endregion Const

    #region Fields

    private readonly System.Net.Sockets.Socket _socket = socket;
    private readonly System.Threading.CancellationTokenSource _cts = new();

    // PooledReceiveContext wraps a PooledSocketAsyncEventArgs from ObjectPoolManager.
    // One context per connection; returned to the pool on Dispose.
    [System.Diagnostics.CodeAnalysis.AllowNull] private IConnection _sender;
    [System.Diagnostics.CodeAnalysis.AllowNull] private IConnectEventArgs _cachedArgs;
    [System.Diagnostics.CodeAnalysis.AllowNull] private PooledSocketReceiveContext _recvCtx;
    [System.Diagnostics.CodeAnalysis.AllowNull] private System.EventHandler<IConnectEventArgs> _callbackPost;
    [System.Diagnostics.CodeAnalysis.AllowNull] private System.EventHandler<IConnectEventArgs> _callbackClose;
    [System.Diagnostics.CodeAnalysis.AllowNull] private System.EventHandler<IConnectEventArgs> _callbackProcess;

    private System.Int32 _pendingProcessCallbacks;

    private System.Int32 _disposed;        // 0 = no, 1 = yes
    private System.Int32 _closeSignaled;
    private System.Int32 _receiveStarted;  // 0 = not yet, 1 = started
    private System.Int32 _cancelSignaled;  // 0 = not yet, 1 = started

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    // Receive buffer — owned by this connection during its lifetime.
    // Swapped atomically when a larger packet arrives (rare).
    [System.Diagnostics.CodeAnalysis.AllowNull]
    private System.Byte[] buffer = BufferLease.ByteArrayPool.Rent();

    #endregion Fields

    #region Properties

    /// <summary>Caches incoming packets.</summary>
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    public FramedSocketCache Cache { get; } = new();

    /// <summary>
    /// Returns the number of packets dispatched to <see cref="AsyncCallback"/>
    /// that have not yet been processed by the protocol handler.
    /// Used by diagnostics and the per-connection throttle check.
    /// </summary>
    public System.Int32 PendingPackets
        => System.Threading.Volatile.Read(ref _pendingProcessCallbacks);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Registers the callbacks and state required before sending or receiving.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_sender), nameof(_cachedArgs))]
    public void SetCallback(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args,
        [System.Diagnostics.CodeAnalysis.AllowNull] System.EventHandler<IConnectEventArgs> close,
        [System.Diagnostics.CodeAnalysis.AllowNull] System.EventHandler<IConnectEventArgs> post,
        [System.Diagnostics.CodeAnalysis.AllowNull] System.EventHandler<IConnectEventArgs> process)
    {
        _callbackPost = post;
        _callbackClose = close;
        _callbackProcess = process;

        _sender = sender ?? throw new System.ArgumentNullException(nameof(sender));
        _cachedArgs = args ?? throw new System.ArgumentNullException(nameof(args));

#if DEBUG
        s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(SetCallback)}] " +
                        $"configured ep={_socket.RemoteEndPoint}");
#endif
    }

    /// <summary>
    /// Called by the protocol handler (via the wrapped process callback in Connection.cs)
    /// after each packet has been fully processed. Decrements the per-connection pending
    /// counter so the receive loop can accept the next packet from this connection.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void OnPacketProcessed() => System.Threading.Interlocked.Decrement(ref _pendingProcessCallbacks);

    /// <summary>
    /// Starts the SAEA-backed receive loop exactly once.
    /// The optional <paramref name="cancellationToken"/> participates in cooperative shutdown.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>")]
    public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
    {
        THROW_IF_NOT_CONFIGURED();

        if (System.Threading.Volatile.Read(ref _disposed) != 0)
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(BeginReceive)}] " +
                            $"skip — already disposed ep={_socket.RemoteEndPoint}");
#endif
            return;
        }

        // Guard: start exactly once.
        if (System.Threading.Interlocked.CompareExchange(ref _receiveStarted, 1, 0) != 0)
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(BeginReceive)}] " +
                            $"skip — already started ep={_socket.RemoteEndPoint}");
#endif
            return;
        }

        // Acquire PooledReceiveContext from ObjectPoolManager — same pattern as
        // PooledAcceptContext usage in the accept loop.
        _recvCtx = s_pool.Get<PooledSocketReceiveContext>();
        _recvCtx.EnsureArgsBound();

#if DEBUG
        s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(BeginReceive)}] " +
                        $"saea-receive-loop started ep={_socket.RemoteEndPoint}");
#endif

        System.Threading.CancellationTokenSource linked =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _ = SAEA_RECEIVE_LOOP_ASYNC(linked.Token).ContinueWith(static (t, state) =>
        {
            (ILogger l, System.Threading.CancellationTokenSource link) =
                ((ILogger, System.Threading.CancellationTokenSource))state!;

            if (t.IsFaulted)
            {
                l?.Error($"[NW.{nameof(FramedSocketConnection)}:{nameof(BeginReceive)}] saea-receive-loop faulted", t.Exception!);
            }

            link.Dispose();
        }, (s_logger, linked), System.Threading.Tasks.TaskScheduler.Default);
    }

    /// <summary>
    /// Sends data synchronously.
    /// Small packets (≤ <see cref="PacketConstants.StackAllocLimit"/>) are framed on the
    /// stack; larger ones use a pooled heap buffer.
    /// </summary>
    /// <returns><see langword="true"/> if the data was sent successfully.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean Send(System.ReadOnlySpan<System.Byte> data)
    {
        THROW_IF_NOT_CONFIGURED();

        if (System.Threading.Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > PacketConstants.PacketSizeLimit - HeaderSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(data),
                $"Packet size {data.Length} exceeds limit {PacketConstants.PacketSizeLimit - HeaderSize}");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + HeaderSize);

        // ── Fast path: stack-allocate frame for small packets ─────────────
        if (data.Length <= PacketConstants.StackAllocLimit)
        {
            try
            {
#if DEBUG
                s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                                $"stackalloc len={data.Length} ep={_socket.RemoteEndPoint}");
#endif
                System.Span<System.Byte> frameS = stackalloc System.Byte[totalLength];
                WRITE_FRAME_HEADER(frameS, totalLength, data);

                // Debug: log payload hex (truncated) when logger exists.
                if (s_logger is not null)
                {
                    var payloadSpan = frameS.Slice(HeaderSize, data.Length);
                    s_logger.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                                   $"sending frame totalLen={totalLength} payload={FORMAT_FRAME_FOR_LOG(payloadSpan)} ep={_socket.RemoteEndPoint}");
                }

                System.Int32 sent = 0;
                while (sent < frameS.Length)
                {
                    System.Int32 n = _socket.Send(frameS[sent..]);
                    if (n == 0)
                    {
#if DEBUG
                        s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                                        $"stackalloc peer-closed ep={_socket.RemoteEndPoint}");
#endif
                        CANCEL_RECEIVE_ONCE();
                        INVOKE_CLOSE_ONCE();
                        return false;
                    }
                    sent += n;
                }

                ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
                args.Initialize(_cachedArgs.Connection);

                AsyncCallback.Invoke(_callbackPost, _sender!, args);
                return true;
            }
            catch (System.Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                                $"stackalloc-error ep={_socket.RemoteEndPoint}", ex);
                return false;
            }
        }

        // ── Slow path: pooled heap buffer ──────────────────────────────────
        System.Byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);
        try
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                            $"pooled len={data.Length} ep={_socket.RemoteEndPoint}");
#endif
            System.Buffers.Binary.BinaryPrimitives
                .WriteUInt16LittleEndian(System.MemoryExtensions.AsSpan(heapBuf), totalLength);
            data.CopyTo(System.MemoryExtensions.AsSpan(heapBuf, HeaderSize));

            // Debug: log payload hex (truncated) when logger exists.
            if (s_logger is not null)
            {
                var payloadSpan = System.MemoryExtensions.AsSpan(heapBuf, HeaderSize, data.Length);
                s_logger.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                               $"sending frame totalLen={totalLength} payload={FORMAT_FRAME_FOR_LOG(payloadSpan)} " +
                               $"ep={_socket.RemoteEndPoint}");
            }

            System.Int32 sent = 0;
            while (sent < totalLength)
            {
                System.Int32 n = _socket.Send(heapBuf, sent, totalLength - sent,
                                              System.Net.Sockets.SocketFlags.None);
                if (n == 0)
                {
#if DEBUG
                    s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                                    $"pooled peer-closed ep={_socket.RemoteEndPoint}");
#endif
                    CANCEL_RECEIVE_ONCE();
                    INVOKE_CLOSE_ONCE();
                    return false;
                }
                sent += n;
            }

            ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
            args.Initialize(_cachedArgs.Connection);

            AsyncCallback.Invoke(_callbackPost, _sender!, args);
            return true;
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(FramedSocketConnection)}:{nameof(Send)}] " +
                            $"pooled-error ep={_socket.RemoteEndPoint}", ex);
            return false;
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
        }
    }

    /// <summary>
    /// Sends data asynchronously. Uses a pooled heap buffer for framing.
    /// </summary>
    /// <returns><see langword="true"/> if the data was sent successfully.</returns>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.ReadOnlyMemory<System.Byte> data,
        System.Threading.CancellationToken cancellationToken)
    {
        THROW_IF_NOT_CONFIGURED();

        if (System.Threading.Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > PacketConstants.PacketSizeLimit - HeaderSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + HeaderSize);
        System.Byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);

        try
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(SendAsync)}] " +
                            $"len={data.Length} ep={_socket.RemoteEndPoint}");
#endif
            WRITE_FRAME_HEADER(System.MemoryExtensions.AsSpan(heapBuf), totalLength, data.Span);

            // Debug: log payload hex (truncated) when logger exists.
            if (s_logger is not null)
            {
                var payloadSpan = data.Span;
                s_logger.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(SendAsync)}] " +
                               $"sending async frame totalLen={totalLength} payload={FORMAT_FRAME_FOR_LOG(payloadSpan)} " +
                               $"ep={_socket.RemoteEndPoint}");
            }

            System.Int32 sent = 0;
            while (sent < totalLength)
            {
                System.Int32 n = await _socket.SendAsync(
                    System.MemoryExtensions.AsMemory(heapBuf, sent, totalLength - sent),
                    System.Net.Sockets.SocketFlags.None, cancellationToken)
                    .ConfigureAwait(false);

                if (n == 0)
                {
#if DEBUG
                    s_logger?.Debug($"[NW.{nameof(FramedSocketConnection)}:{nameof(SendAsync)}] " +
                                    $"peer-closed ep={_socket.RemoteEndPoint}");
#endif
                    CANCEL_RECEIVE_ONCE();
                    INVOKE_CLOSE_ONCE();
                    return false;
                }
                sent += n;
            }

            ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
            args.Initialize(_cachedArgs.Connection);

            AsyncCallback.Invoke(_callbackPost, _sender!, args);
            return true;
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(FramedSocketConnection)}:{nameof(SendAsync)}] " +
                            $"error ep={_socket.RemoteEndPoint}", ex);
            return false;
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
        }
    }

    /// <summary>Sends data synchronously from an <see cref="System.ArraySegment{T}"/>.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Send(System.ArraySegment<System.Byte> segment)
        => segment.Array is not null && Send(new System.ReadOnlySpan<System.Byte>(
            segment.Array, segment.Offset, segment.Count));

    /// <summary>Sends data asynchronously from an <see cref="System.ArraySegment{T}"/>.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.ArraySegment<System.Byte> segment,
        System.Threading.CancellationToken cancellationToken)
        => segment.Array is null
            ? System.Threading.Tasks.Task.FromResult(false)
            : SendAsync(new System.ReadOnlyMemory<System.Byte>(
                segment.Array, segment.Offset, segment.Count), cancellationToken);

    #endregion Public Methods

    #region Dispose Pattern

    /// <summary>Disposes the resources used by this instance.</summary>
    public void Dispose()
    {
        DISPOSE(true);
        System.GC.SuppressFinalize(this);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString()
        => $"FramedSocketConnection (Client={_socket.RemoteEndPoint}, " +
           $"Disposed={System.Threading.Volatile.Read(ref _disposed) != 0}, " +
           $"UpTime={Cache.Uptime}ms, LastPing={Cache.LastPingTime}ms, " +
           $"PendingPackets={PendingPackets}.";

    #endregion Dispose Pattern

    #region Private: SAEA Receive Loop

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes at <paramref name="offset"/>
    /// via <see cref="PooledSocketReceiveContext.ReceiveAsync"/>.
    /// Loops internally to handle partial receives (common under load).
    /// </summary>
    private async System.Threading.Tasks.ValueTask SAEA_RECEIVE_EXACTLY_ASYNC(
        System.Int32 offset,
        System.Int32 count,
        System.Threading.CancellationToken token)
    {
        System.Int32 read = 0;
        while (read < count)
        {
            token.ThrowIfCancellationRequested();

            System.Int32 n = await _recvCtx!
                .ReceiveAsync(_socket, buffer, offset + read, count - read)
                .ConfigureAwait(false);

            if (n == 0)
            {
                throw new System.IO.IOException("Peer closed (FIN)",
                    new System.Net.Sockets.SocketException(
                        (System.Int32)System.Net.Sockets.SocketError.Shutdown));
            }

#if DEBUG
            if (read == 0 && n < count)
            {
                s_logger?.Debug(
                    $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_EXACTLY_ASYNC)}] " +
                    $"partial recv got={n} need={count} offset={offset} ep={_socket.RemoteEndPoint}");
            }
#endif
            read += n;
        }
    }

    /// <summary>
    /// Main receive loop — uses <see cref="PooledSocketReceiveContext"/> (SAEA) for zero-alloc receives.
    ///
    /// <para><b>Layer 1 throttle:</b> before handing a packet off to the cache, this loop
    /// checks <c>_pendingProcessCallbacks</c>. If the connection has
    /// <see cref="MaxPerConnectionPendingPackets"/> packets already queued in
    /// <see cref="AsyncCallback"/> awaiting a ThreadPool thread, the current packet is
    /// dropped and a warning is emitted. The buffer is returned to the pool immediately and
    /// a fresh one is rented so the loop can continue receiving (and discarding) the flood
    /// without stalling or allocating.</para>
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private async System.Threading.Tasks.Task SAEA_RECEIVE_LOOP_ASYNC(
        System.Threading.CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // ── Step 1: read 2-byte little-endian length header ───────
                await SAEA_RECEIVE_EXACTLY_ASYNC(0, HeaderSize, token)
                    .ConfigureAwait(false);

                System.UInt16 size = System.Buffers.Binary.BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        System.MemoryExtensions.AsSpan(buffer, 0, HeaderSize));

#if DEBUG
                s_logger?.Meta(
                    $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                    $"recv-header size(le)={size} ep={_sender?.NetworkEndpoint.Address}");
#endif

                if (!IS_VALID_PACKET_SIZE(size))
                {
#if DEBUG
                    s_logger?.Debug(
                        $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"invalid-size={size} ep={_sender?.NetworkEndpoint.Address}");
#endif
                    throw new System.Net.Sockets.SocketException(
                        (System.Int32)System.Net.Sockets.SocketError.ProtocolNotSupported);
                }

                // ── Step 2: grow buffer only when packet exceeds capacity ──
                if (size > buffer.Length)
                {
#if DEBUG
                    s_logger?.Debug(
                        $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"grow-buffer old={buffer.Length} new={size} ep={_sender?.NetworkEndpoint.Address}");
#endif
                    System.Byte[] oldBuf = buffer;
                    System.Byte[] newBuf = BufferLease.ByteArrayPool.Rent(size);

                    // Preserve the already-read header bytes in the new buffer.
                    System.MemoryExtensions.AsSpan(oldBuf, 0, HeaderSize)
                        .CopyTo(System.MemoryExtensions.AsSpan(newBuf));

                    System.Byte[] swapped =
                        System.Threading.Interlocked.Exchange(ref buffer, newBuf);

                    if (swapped is not null && swapped != newBuf)
                    {
                        BufferLease.ByteArrayPool.Return(swapped);
                    }
                }

                // ── Step 3: read payload bytes ────────────────────────────
                System.Int32 payload = size - HeaderSize;
                await this.SAEA_RECEIVE_EXACTLY_ASYNC(HeaderSize, payload, token)
                          .ConfigureAwait(false);

#if DEBUG
                s_logger?.Debug(
                    $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                    $"recv-frame size={size} payload={payload} ep={_sender?.NetworkEndpoint.Address}");
#endif

                // ── Step 4: Layer 1 per-connection throttle ───────────────
                // Try to reserve a pending slot. If the connection already has
                // MaxPerConnectionPendingPackets in-flight, drop this packet and
                // return the buffer immediately — flood traffic never reaches
                // AsyncCallback or the ThreadPool.
                System.Int32 pending =
                    System.Threading.Interlocked.Increment(ref _pendingProcessCallbacks);

                if (pending > MaxPerConnectionPendingPackets)
                {
                    System.Threading.Interlocked.Decrement(ref _pendingProcessCallbacks);

                    s_logger?.Warn(
                        $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"per-conn-throttle pending={pending} max={MaxPerConnectionPendingPackets} " +
                        $"ep={_sender?.NetworkEndpoint.Address} — packet dropped");

                    // Return buffer to pool — rent a fresh one for next receive.
                    System.Byte[] dropped =
                        System.Threading.Interlocked.Exchange(ref buffer, null!);
                    if (dropped is not null)
                    {
                        BufferLease.ByteArrayPool.Return(dropped);
                    }

                    buffer = BufferLease.ByteArrayPool.Rent();
                    continue;
                }

                // ── Step 5: zero-copy handoff to session cache ────────────
                // Interlocked.Exchange(null) prevents Dispose from double-returning.
                System.Byte[] currentBuf = System.Threading.Interlocked.Exchange(ref buffer, null!);

                if (currentBuf is not null)
                {
                    Cache.LastPingTime = Clock.UnixMillisecondsNow();

                    BufferLease lease = BufferLease.TakeOwnership(currentBuf, HeaderSize, payload);
                    lease.Retain(); // Retain for the callback; released in Connection.cs after processing.

                    ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
                    args.Initialize(lease, _cachedArgs.Connection);

#if DEBUG
                    System.Boolean queued = AsyncCallback.Invoke(_callbackProcess, _sender, args);
                    s_logger?.Debug(
                        $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"handoff-to-cache payload={payload} pending={pending} ep={_sender?.NetworkEndpoint.Address} " +
                        $"callback-queued={queued}");
#else
                    AsyncCallback.Invoke(_callbackProcess, _sender, args);
#endif

#if DEBUG
                    s_logger?.Debug(
                        $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"handoff-to-cache payload={payload} pending={pending} ep={_sender?.NetworkEndpoint.Address}");
#endif
                }
                else
                {
                    // Buffer was swapped out by Dispose racing with the loop.
                    // The increment must be undone since no callback will fire.
                    System.Threading.Interlocked.Decrement(ref _pendingProcessCallbacks);
                }

                // Rent a fresh buffer for the next receive.
                buffer = BufferLease.ByteArrayPool.Rent();
            }
        }
        catch (System.Exception ex) when (IS_BENIGN_DISCONNECT(ex))
        {
            s_logger?.Trace(
                $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                $"ended (peer closed/shutdown) ep={_sender?.NetworkEndpoint.Address}");
        }
        catch (System.OperationCanceledException)
        {
            s_logger?.Trace(
                $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                $"cancelled ep={_sender?.NetworkEndpoint.Address}");
        }
        catch (System.Exception ex)
        {
            System.Exception e = (ex as System.AggregateException)?.Flatten() ?? ex;
            s_logger?.Error(
                $"[NW.{nameof(FramedSocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                $"faulted ep={_sender?.NetworkEndpoint.Address}", e);
        }
        finally
        {
            CANCEL_RECEIVE_ONCE();
            INVOKE_CLOSE_ONCE();
        }
    }

    #endregion Private: SAEA Receive Loop

    #region Private Methods

    [System.Diagnostics.DebuggerStepThrough]
    private void DISPOSE(System.Boolean disposing)
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            // 1. Signal cancellation so the receive loop exits cleanly.
            CANCEL_RECEIVE_ONCE();

            // 2. Shutdown and close the socket (causes in-flight SAEA to abort,
            //    which lets PooledReceiveContext._idle become signaled).
            try
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                }
            }
            catch { /* ignore */ }

            try { _socket.Close(); } catch { /* ignore */ }

            // 3. Return PooledReceiveContext to ObjectPoolManager.
            if (_recvCtx is not null)
            {
                _recvCtx.ResetForPool();
                s_pool.Return<PooledSocketReceiveContext>(_recvCtx);
                _recvCtx = null;
            }

            // 4. Return the receive buffer (Interlocked prevents double-return).
            System.Byte[] bufToReturn =
                System.Threading.Interlocked.Exchange(ref buffer, null!);
            if (bufToReturn is not null)
            {
                BufferLease.ByteArrayPool.Return(bufToReturn);
            }

            // 6. Fire the close callback.
            INVOKE_CLOSE_ONCE();

            // 7. Dispose remaining resources.
            _cts.Dispose();
            _socket.Dispose();
        }

#if DEBUG
        s_logger?.Debug(
            $"[NW.{nameof(FramedSocketConnection)}:{nameof(Dispose)}] " +
            $"disposed ep={FORMAT_ENDPOINT(_socket)}");
#endif
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void WRITE_FRAME_HEADER(
        System.Span<System.Byte> buffer,
        System.UInt16 totalLength,
        System.ReadOnlySpan<System.Byte> payload)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buffer, totalLength);
        payload.CopyTo(buffer[HeaderSize..]);
    }

    private static System.Boolean IS_VALID_PACKET_SIZE(System.UInt16 size)
        => size is >= HeaderSize and <= PacketConstants.PacketSizeLimit;

    [System.Diagnostics.DebuggerStepThrough]
    private static System.String FORMAT_ENDPOINT(System.Net.Sockets.Socket s)
    {
        try { return s.RemoteEndPoint?.ToString() ?? "<unknown>"; }
        catch (System.ObjectDisposedException) { return "<disposed>"; }
        catch { return "<unknown>"; }
    }

    [System.Diagnostics.DebuggerStepThrough]
    private static System.Boolean IS_BENIGN_DISCONNECT(System.Exception ex)
    {
        if (ex is System.OperationCanceledException or System.ObjectDisposedException)
        {
            return true;
        }

        if (ex is System.Net.Sockets.SocketException se)
        {
            return se.SocketErrorCode
                is System.Net.Sockets.SocketError.ConnectionReset
                or System.Net.Sockets.SocketError.ConnectionAborted
                or System.Net.Sockets.SocketError.Shutdown
                or System.Net.Sockets.SocketError.OperationAborted;
        }

        if (ex is System.IO.IOException ioex &&
            ioex.InnerException is System.Net.Sockets.SocketException ise)
        {
            return ise.SocketErrorCode
                is System.Net.Sockets.SocketError.ConnectionReset
                or System.Net.Sockets.SocketError.ConnectionAborted
                or System.Net.Sockets.SocketError.Shutdown
                or System.Net.Sockets.SocketError.OperationAborted;
        }

        if (ex is System.AggregateException agg)
        {
            agg = agg.Flatten();
            foreach (System.Exception inner in agg.InnerExceptions)
            {
                if (!IS_BENIGN_DISCONNECT(inner))
                {
                    return false;
                }
            }
            return agg.InnerExceptions.Count > 0;
        }

        return false;
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void INVOKE_CLOSE_ONCE()
    {
        if (System.Threading.Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(_cachedArgs.Connection);

        AsyncCallback.Invoke(_callbackClose, _sender!, args);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CANCEL_RECEIVE_ONCE()
    {
        if (System.Threading.Interlocked.Exchange(ref _cancelSignaled, 1) != 0)
        {
            return;
        }

        try { _cts.Cancel(); } catch { /* ignore */ }
    }

    private void THROW_IF_NOT_CONFIGURED()
    {
        if (_sender is null || _cachedArgs is null)
        {
            throw new System.InvalidOperationException("SetCallback must be called before use");
        }
    }

    private static System.String FORMAT_FRAME_FOR_LOG(System.ReadOnlySpan<System.Byte> payload, System.Int32 maxBytes = 64)
    {
        if (payload.IsEmpty)
        {
            return "<empty>";
        }

        System.Int32 show = payload.Length > maxBytes ? maxBytes : payload.Length;
        System.String hex = System.Convert.ToHexString(payload[..show]);
        if (payload.Length > show)
        {
            hex += "...";
        }

        return $"len={payload.Length} hex={hex}";
    }

    #endregion Private Methods
}