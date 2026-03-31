// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooling;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
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
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("{ToString()}")]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed partial class SocketConnection(Socket socket) : IDisposable
{
    #region Const

    private const byte HeaderSize = sizeof(ushort);

    #endregion Const

    #region Fields

    private readonly Socket _socket = socket;
    private readonly CancellationTokenSource _cts = new();
    private readonly FragmentAssembler _fragmentAssembler = new();

    /// <summary>
    /// PooledReceiveContext wraps a PooledSocketAsyncEventArgs from ObjectPoolManager.
    /// One context per connection; returned to the pool on Dispose.
    /// </summary>
    private IConnection _sender = null!;
    private IConnectEventArgs _cachedArgs = null!;
    private PooledSocketReceiveContext _recvCtx = null!;
    private EventHandler<IConnectEventArgs>? _callbackPost;
    private EventHandler<IConnectEventArgs>? _callbackClose;
    private EventHandler<IConnectEventArgs>? _callbackProcess;

    private int _packetCount;
    private int _openFragmentStreams;
    private int _pendingProcessCallbacks;

    /// <summary>
    /// 0 = no, 1 = yes
    /// </summary>
    private int _disposed;
    private int _closeSignaled;
    /// <summary>
    /// 0 = not yet, 1 = started
    /// </summary>
    private int _receiveStarted;
    /// <summary>
    /// 0 = not yet, 1 = started
    /// </summary>
    private int _cancelSignaled;

    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
    private static readonly FragmentOptions s_fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();

    /// <summary>
    /// Receive buffer — owned by this connection during its lifetime.
    /// Swapped atomically when a larger packet arrives (rare).
    /// </summary>
    private byte[] _buffer = BufferLease.ByteArrayPool.Rent();

    #endregion Fields

    #region Options

    /// <summary>
    /// Loaded once at startup from NetworkCallbackOptions via ConfigurationManager.
    /// All throttle values are read from config so they can be tuned without recompile.
    /// </summary>
    private static readonly NetworkCallbackOptions s_opts = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();

    #endregion Options

    #region Properties

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public long Uptime { get => (long)Clock.UnixTime().TotalMilliseconds - field; } = (long)Clock.UnixTime().TotalMilliseconds;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public long LastPingTime
    {
        get => Interlocked.Read(ref field);
        set => Interlocked.Exchange(ref field, value);
    }

    /// <summary>
    /// Returns the number of packets dispatched to <see cref="AsyncCallback"/>
    /// that have not yet been processed by the protocol handler.
    /// Used by diagnostics and the per-connection throttle check.
    /// </summary>
    public int PendingPackets => Volatile.Read(ref _pendingProcessCallbacks);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Registers the callbacks and state required before sending or receiving.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    /// <param name="close"></param>
    /// <param name="post"></param>
    /// <param name="process"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [MemberNotNull(nameof(_sender), nameof(_cachedArgs))]
    public void SetCallback(
        IConnection sender,
        IConnectEventArgs args,
        EventHandler<IConnectEventArgs>? close,
        EventHandler<IConnectEventArgs>? post,
        EventHandler<IConnectEventArgs>? process)
    {
        _callbackPost = post;
        _callbackClose = close;
        _callbackProcess = process;

        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _cachedArgs = args ?? throw new ArgumentNullException(nameof(args));

#if DEBUG
        s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(SetCallback)}] " +
                        $"configured ep={_socket.RemoteEndPoint}");
#endif
    }

    /// <summary>
    /// Called by the protocol handler (via the wrapped process callback in Connection.cs)
    /// after each packet has been fully processed. Decrements the per-connection pending
    /// counter so the receive loop can accept the next packet from this connection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnPacketProcessed() => Interlocked.Decrement(ref _pendingProcessCallbacks);

    /// <summary>
    /// Starts the SAEA-backed receive loop exactly once.
    /// The optional <paramref name="cancellationToken"/> participates in cooperative shutdown.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage(
        "Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>")]
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        this.THROW_IF_NOT_CONFIGURED();

        if (Volatile.Read(ref _disposed) != 0)
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(BeginReceive)}] " +
                            $"skip — already disposed ep={_socket.RemoteEndPoint}");
#endif
            return;
        }

        // Guard: start exactly once.
        if (Interlocked.CompareExchange(ref _receiveStarted, 1, 0) != 0)
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(BeginReceive)}] " +
                            $"skip — already started ep={_socket.RemoteEndPoint}");
#endif
            return;
        }

        // Acquire PooledReceiveContext from ObjectPoolManager — same pattern as
        // PooledAcceptContext usage in the accept loop.
        _recvCtx = s_pool.Get<PooledSocketReceiveContext>();
        _recvCtx.EnsureArgsBound();

#if DEBUG
        s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(BeginReceive)}] " +
                        $"saea-receive-loop started ep={_socket.RemoteEndPoint}");
#endif

        CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _ = this.SAEA_RECEIVE_LOOP_ASYNC(linked.Token).ContinueWith(static (t, state) =>
        {
            (ILogger l, CancellationTokenSource link) =
                ((ILogger, CancellationTokenSource))state!;

            if (t.IsFaulted)
            {
                l?.Error($"[NW.{nameof(SocketConnection)}:{nameof(BeginReceive)}] saea-receive-loop faulted", t.Exception!);
            }

            link.Dispose();
        }, (s_logger, linked), TaskScheduler.Default);
    }

    #endregion Public Methods

    #region Dispose Pattern

    /// <summary>Disposes the resources used by this instance.</summary>
    public void Dispose()
    {
        this.DISPOSE(true);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
        => $"FramedSocketConnection (Client={_socket.RemoteEndPoint}, " +
           $"Disposed={Volatile.Read(ref _disposed) != 0}, " +
           $"UpTime={this.Uptime}ms, LastPing={this.LastPingTime}ms, " +
           $"PendingPackets={this.PendingPackets}, " +
           $"OpenFragmentStreams={Volatile.Read(ref _openFragmentStreams)}.";

    #endregion Dispose Pattern

    #region Private: SAEA Receive Loop

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes at <paramref name="offset"/>
    /// via <see cref="PooledSocketReceiveContext.ReceiveAsync"/>.
    /// Loops internally to handle partial receives (common under load).
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <param name="token"></param>
    /// <exception cref="IOException"></exception>
    private async ValueTask SAEA_RECEIVE_EXACTLY_ASYNC(
        int offset,
        int count,
        CancellationToken token)
    {
        int read = 0;
        while (read < count)
        {
            token.ThrowIfCancellationRequested();

            int n;
            ValueTask<int> vt = _recvCtx.ReceiveAsync(_socket, _buffer, offset + read, count - read);

            if (vt.IsCompletedSuccessfully)  // synchronous path (hot path)
            {
                n = vt.Result;
            }
            else
            {
                n = await vt.ConfigureAwait(false);
            }

            if (n == 0)
            {
                throw new NetworkException(
                    $"Connection closed (FIN): read={read}, required={count}, endpoint={_socket.RemoteEndPoint}.",
                    new SocketException((int)SocketError.ConnectionReset));
            }

#if DEBUG
            if (read == 0 && n < count)
            {
                s_logger?.Debug(
                    $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_EXACTLY_ASYNC)}] " +
                    $"partial recv: got={n}, need={count}, offset={offset}, ep={_socket.RemoteEndPoint}");
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
    /// <see cref="NetworkCallbackOptions.MaxPerConnectionPendingPackets"/> packets already queued in
    /// <see cref="AsyncCallback"/> awaiting a ThreadPool thread, the current packet is
    /// dropped and a warning is emitted. The buffer is returned to the pool immediately and
    /// a fresh one is rented so the loop can continue receiving (and discarding) the flood
    /// without stalling or allocating.</para>
    /// </summary>
    /// <param name="token"></param>
    /// <exception cref="SocketException"></exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task SAEA_RECEIVE_LOOP_ASYNC(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // ── Step 1: read 2-byte little-endian length header ───────
                await this.SAEA_RECEIVE_EXACTLY_ASYNC(0, HeaderSize, token).ConfigureAwait(false);

                ushort size = BinaryPrimitives.ReadUInt16LittleEndian(MemoryExtensions
                                              .AsSpan(_buffer, 0, HeaderSize));

#if DEBUG
                s_logger?.Trace(
                    $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                    $"recv-header size(le)={size} ep={_sender?.NetworkEndpoint.Address}");
#endif

                if (!IS_VALID_PACKET_SIZE(size))
                {
#if DEBUG
                    s_logger?.Debug(
                        $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"invalid-size={size} ep={_sender?.NetworkEndpoint.Address}");
#endif
                    throw new SocketException(
                        (int)SocketError.ProtocolNotSupported);
                }

                // ── Step 2: grow buffer only when packet exceeds capacity ──
                if (size > _buffer.Length)
                {
#if DEBUG
                    s_logger?.Debug(
                        $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"grow-buffer old={_buffer.Length} new={size} ep={_sender?.NetworkEndpoint.Address}");
#endif
                    byte[] oldBuf = _buffer;
                    byte[] newBuf = BufferLease.ByteArrayPool.Rent(size);

                    // Preserve the already-read header bytes in the new buffer.
                    MemoryExtensions.AsSpan(oldBuf, 0, HeaderSize)
                                    .CopyTo(MemoryExtensions
                                    .AsSpan(newBuf));

                    byte[] swapped = Interlocked.Exchange(ref _buffer, newBuf);

                    if (swapped is not null && swapped != newBuf)
                    {
                        BufferLease.ByteArrayPool.Return(swapped);
                    }
                }

                // ── Step 3: read payload bytes ────────────────────────────
                int payload = size - HeaderSize;
                await this.SAEA_RECEIVE_EXACTLY_ASYNC(HeaderSize, payload, token).ConfigureAwait(false);

#if DEBUG
                s_logger?.Debug(
                    $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                    $"recv-frame size={size} payload={payload} ep={_sender?.NetworkEndpoint.Address}");
#endif
                // ── Step 3.5: periodic eviction of stale fragment streams ───────
                if ((++_packetCount & (FragmentAssembler.EvictInterval - 1)) == 0)
                {
                    int evicted = _fragmentAssembler.EvictExpired();
                    if (evicted > 0)
                    {
                        Interlocked.Add(ref _openFragmentStreams, -evicted);
                        s_logger?.Warn(
                            $"[NW.{nameof(SocketConnection)}] " +
                            $"evicted {evicted} stale fragment stream(s) " +
                            $"ep={_sender?.NetworkEndpoint.Address}");
                    }
                }

                // ── Step 4: Layer 1 per-connection throttle ───────────────
                // Try to reserve a pending slot. If the connection already has
                // MaxPerConnectionPendingPackets in-flight, drop this packet and
                // return the buffer immediately — flood traffic never reaches
                // AsyncCallback or the ThreadPool.
                int pending = Interlocked.Increment(ref _pendingProcessCallbacks);

                if (pending > s_opts.MaxPerConnectionPendingPackets)
                {
                    Interlocked.Decrement(ref _pendingProcessCallbacks);

                    s_logger?.Warn(
                        $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                        $"per-conn-throttle pending={pending} max={s_opts.MaxPerConnectionPendingPackets} " +
                        $"ep={_sender?.NetworkEndpoint.Address} — packet dropped");

                    // Return buffer to pool — rent a fresh one for next receive.
                    byte[]? dropped = Interlocked.Exchange(ref _buffer, null!);
                    if (dropped is not null)
                    {
                        BufferLease.ByteArrayPool.Return(dropped);
                    }

                    _buffer = BufferLease.ByteArrayPool.Rent();
                    continue;
                }

                // ── Step 5: zero-copy handoff to session cache ────────────
                // Interlocked.Exchange(null) prevents Dispose from double-returning.
                byte[]? currentBuf = Interlocked.Exchange(ref _buffer, null!);

                if (currentBuf is not null)
                {
                    this.LastPingTime = Clock.UnixMillisecondsNow();
                    BufferLease lease = BufferLease.TakeOwnership(currentBuf, HeaderSize, payload);
                    ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
                    ReadOnlySpan<byte> payloadSpan = lease.Span;

                    if (FragmentAssembler.IsFragmentedFrame(payloadSpan, out FragmentHeader header))
                    {
                        ReadOnlySpan<byte> chunkBody = payloadSpan[FragmentHeader.WireSize..];

                        if (header.ChunkIndex == 0)
                        {
                            int openStreams = Interlocked.Increment(ref _openFragmentStreams);

                            if (openStreams > s_opts.MaxPerConnectionOpenFragmentStreams)
                            {
                                Interlocked.Decrement(ref _openFragmentStreams);
                                s_logger?.Trace($"[[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                                                $"fragment-stream-limit open={openStreams} — stream dropped");

                                Interlocked.Decrement(ref _pendingProcessCallbacks);
                                lease.Dispose();
                                args.Dispose();
                                _buffer = BufferLease.ByteArrayPool.Rent();

                                continue;
                            }
                        }

#if DEBUG
                        s_logger?.Debug(
                            $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                            $"recv-fragment stream={header.StreamId} chunk={header.ChunkIndex}/{header.TotalChunks} " +
                            $"isLast={header.IsLast} bodyLen={chunkBody.Length} ep={_sender?.NetworkEndpoint.Address}");
#endif

                        BufferLease? assembled = _fragmentAssembler.Add(header, chunkBody, out bool streamEvicted);

                        if (assembled is not null)
                        {
                            assembled.Retain();
                            args.Initialize(assembled, _cachedArgs.Connection);
                            AsyncCallback.Invoke(_callbackProcess, _sender, args);
#if DEBUG
                            s_logger?.Debug(
                                $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                                $"fragment-assembled stream={header.StreamId} totalLen={assembled.Length} " +
                                $"ep={_sender?.NetworkEndpoint.Address}");
#endif
                            // If this was the last chunk, the assembler has no more use for the stream and can release it.
                            Interlocked.Decrement(ref _openFragmentStreams);
                        }
                        else
                        {
                            args.Dispose();
                            Interlocked.Decrement(ref _pendingProcessCallbacks);

                            if (streamEvicted)
                            {
                                Interlocked.Decrement(ref _openFragmentStreams);
                            }
                        }

                        lease.Dispose();
#if DEBUG
                        s_logger?.Debug(
                            $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                            $"handoff-to-cache payload={payload} pending={pending} ep={_sender?.NetworkEndpoint.Address}");
#endif
                    }
                    else
                    {
                        lease.Retain();
                        args.Initialize(lease, _cachedArgs.Connection);
                        bool queued = AsyncCallback.Invoke(_callbackProcess, _sender, args);
#if DEBUG
                        s_logger?.Debug(
                            $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                            $"handoff-to-cache payload={payload} pending={pending} ep={_sender?.NetworkEndpoint.Address} callback-queued={queued}");
#endif
                    }
                }
                else
                {
                    // Buffer was swapped out by Dispose racing with the loop.
                    Interlocked.Decrement(ref _pendingProcessCallbacks);
                }

                // Rent a fresh buffer for the next receive.
                _buffer = BufferLease.ByteArrayPool.Rent();
            }
        }
        catch (Exception ex) when (IS_BENIGN_DISCONNECT(ex))
        {
            s_logger?.Trace(
                $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                $"ended (peer closed/shutdown) ep={_sender?.NetworkEndpoint.Address}");
        }
        catch (OperationCanceledException)
        {
            s_logger?.Trace(
                $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                $"cancelled ep={_sender?.NetworkEndpoint.Address}");
        }
        catch (Exception ex)
        {
            Exception e = (ex as AggregateException)?.Flatten() ?? ex;
            s_logger?.Error(
                $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                $"faulted ep={_sender?.NetworkEndpoint.Address}", e);
        }
        finally
        {
            this.CANCEL_RECEIVE_ONCE();
            this.INVOKE_CLOSE_ONCE();
        }
    }

    #endregion Private: SAEA Receive Loop

    #region Private Methods

    [DebuggerStepThrough]
    private void DISPOSE(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            // 1. Signal cancellation so the receive loop exits cleanly.
            this.CANCEL_RECEIVE_ONCE();

            // 2. Shutdown and close the socket (causes in-flight SAEA to abort,
            //    which lets PooledReceiveContext._idle become signaled).
            try
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch { /* ignore */ }

            try { _socket.Close(); } catch { /* ignore */ }

            // 3. Return PooledReceiveContext to ObjectPoolManager.
            if (_recvCtx is not null)
            {
                s_pool.Return(_recvCtx);

                _recvCtx = null!;
            }

            // 4. Return the receive buffer (Interlocked prevents double-return).
            byte[]? bufToReturn =
                Interlocked.Exchange(ref _buffer, null!);
            if (bufToReturn is not null)
            {
                BufferLease.ByteArrayPool.Return(bufToReturn);
            }

            // 6. Fire the close callback.
            this.INVOKE_CLOSE_ONCE();

            // 7. Dispose remaining resources.
            _cts.Dispose();
            _socket.Dispose();
            _fragmentAssembler.Dispose();
        }

#if DEBUG
        s_logger?.Trace(
            $"[NW.{nameof(SocketConnection)}:{nameof(Dispose)}] " +
            $"disposed ep={FORMAT_ENDPOINT(_socket)}");
#endif
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WRITE_FRAME_HEADER(
        Span<byte> buffer,
        ushort totalLength,
        ReadOnlySpan<byte> payload)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, totalLength);
        payload.CopyTo(buffer[HeaderSize..]);
    }

    private static bool IS_VALID_PACKET_SIZE(uint size)
        => size is >= HeaderSize and <= PacketConstants.PacketSizeLimit;

    [DebuggerStepThrough]
    private static string FORMAT_ENDPOINT(Socket s)
    {
        try { return s.RemoteEndPoint?.ToString() ?? "<unknown>"; }
        catch (ObjectDisposedException) { return "<disposed>"; }
        catch { return "<unknown>"; }
    }

    [DebuggerStepThrough]
    private static bool IS_BENIGN_DISCONNECT(Exception ex)
    {
        if (ex is OperationCanceledException or ObjectDisposedException)
        {
            return true;
        }

        if (ex is NetworkException netEx && netEx.InnerException != null)
        {
            return IS_BENIGN_DISCONNECT(netEx.InnerException);
        }

        if (ex is SocketException se)
        {
            return se.SocketErrorCode
                is SocketError.ConnectionReset
                or SocketError.ConnectionAborted
                or SocketError.Shutdown
                or SocketError.OperationAborted;
        }

        if (ex is IOException ioex &&
            ioex.InnerException is SocketException ise)
        {
            return ise.SocketErrorCode
                is SocketError.ConnectionReset
                or SocketError.ConnectionAborted
                or SocketError.Shutdown
                or SocketError.OperationAborted;
        }

        if (ex is AggregateException agg)
        {
            agg = agg.Flatten();
            foreach (Exception inner in agg.InnerExceptions)
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

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void INVOKE_CLOSE_ONCE()
    {
        if (Interlocked.Exchange(ref _closeSignaled, 1) != 0)
        {
            return;
        }

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(_cachedArgs.Connection);

        _ = AsyncCallback.Invoke(_callbackClose, _sender, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CANCEL_RECEIVE_ONCE()
    {
        if (Interlocked.Exchange(ref _cancelSignaled, 1) != 0)
        {
            return;
        }

        try { _cts.Cancel(); } catch { /* ignore */ }
    }

    private void THROW_IF_NOT_CONFIGURED()
    {
        if (_sender is null || _cachedArgs is null)
        {
            throw new InternalErrorException("SetCallback must be called before use");
        }
    }

#if DEBUG
    private static string FORMAT_FRAME_FOR_LOG(ReadOnlySpan<byte> payload, int maxBytes = 64)
    {
        if (payload.IsEmpty)
        {
            return "<empty>";
        }

        int show = payload.Length > maxBytes ? maxBytes : payload.Length;
        string hex = Convert.ToHexString(payload[..show]);
        if (payload.Length > show)
        {
            hex += "...";
        }

        return $"len={payload.Length} hex={hex}";
    }
#endif

    #endregion Private Methods
}
