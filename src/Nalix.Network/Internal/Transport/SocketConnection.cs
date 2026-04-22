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
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Options;

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
/// <param name="logger"></param>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("{ToString()}")]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed partial class SocketConnection(Socket socket, ILogger? logger = null) : IDisposable
{
    #region Const

    private const byte HeaderSize = sizeof(ushort);

    #endregion Const

    #region Fields

    private readonly Socket _socket = socket;
    private readonly ILogger? _logger = logger;
    private FragmentAssembler? _fragmentAssembler;

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
    private Task? _receiveLoopTask;

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

    private static readonly FragmentOptions s_fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    /// <summary>
    /// Persistent receive buffer for opportunistic reads. 
    /// Rented once for the lifetime of the connection.
    /// </summary>
    private byte[]? _buffer = BufferLease.ByteArrayPool.Rent(s_fragmentOptions.MaxChunkSize <= 0 ? 4096 : s_fragmentOptions.MaxChunkSize * 2);

    private int _bufferDataLength;
    private string _endpointString = "<unknown>";

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
    } = Clock.UnixMillisecondsNow();

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

        // Cache endpoint string now while socket is alive — avoids ObjectDisposedException later.
        _endpointString = FORMAT_ENDPOINT(_socket);

#if DEBUG
        _logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(SetCallback)}] " +
                        $"configured ep={_endpointString}");
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
    /// Manually increments the per-connection pending counter.
    /// Used by InjectIncoming in testing scenarios.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void IncrementPendingCallbacks() => Interlocked.Increment(ref _pendingProcessCallbacks);

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
            _logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(BeginReceive)}] " +
                            $"skip — already disposed ep={_endpointString}");
#endif
            return;
        }

        // Guard: start exactly once.
        if (Interlocked.CompareExchange(ref _receiveStarted, 1, 0) != 0)
        {
#if DEBUG
            _logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(BeginReceive)}] " +
                            $"skip — already started ep={_endpointString}");
#endif
            return;
        }

        // Acquire PooledReceiveContext from ObjectPoolManager — same pattern as
        // PooledAcceptContext usage in the accept loop.
        _recvCtx = s_pool.Get<PooledSocketReceiveContext>();
        _recvCtx.EnsureArgsBound();

#if DEBUG
        _logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(BeginReceive)}] " +
                        $"saea-receive-loop started ep={_endpointString}");
#endif

        _receiveLoopTask = this.SAEA_RECEIVE_LOOP_ASYNC(cancellationToken);
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
        => $"FramedSocketConnection (Client={_endpointString}, " +
           $"Disposed={Volatile.Read(ref _disposed) != 0}, " +
           $"UpTime={this.Uptime}ms, LastPing={this.LastPingTime}ms, " +
           $"PendingPackets={this.PendingPackets}, " +
           $"OpenFragmentStreams={Volatile.Read(ref _openFragmentStreams)}.";

    #endregion Dispose Pattern

    #region Private: SAEA Receive Loop

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
            // The opportunistic loop: read as much as possible, then parse as many frames as possible.
            while (Volatile.Read(ref _disposed) == 0 && !token.IsCancellationRequested)
            {
                // Step 1: Parse all complete frames currently in the buffer.
                int consumed = 0;
                bool parsedAtLeastOne = false;

                while (_bufferDataLength - consumed >= HeaderSize)
                {
                    // Peek at the length header (2 bytes LE).
                    ushort size = BinaryPrimitives.ReadUInt16LittleEndian(MemoryExtensions
                                                  .AsSpan(_buffer!, consumed, HeaderSize));

                    if (!IS_VALID_PACKET_SIZE(size))
                    {
#if DEBUG
                        if (_logger?.IsEnabled(LogLevel.Debug) == true)
                        {
                            _logger.Debug($"[NW.{nameof(SocketConnection)}] invalid-size={size} ep={_endpointString}");
                        }
#endif
                        throw NetworkErrors.ProtocolNotSupported;
                    }

                    // Check if the full frame (header + payload) is present in the buffer.
                    if (_bufferDataLength - consumed < size)
                    {
                        // Current frame is incomplete. Break and wait for more data.
                        break;
                    }

                    // Dispatch complete frame.
                    int payloadLen = size - HeaderSize;
                    this.PROCESS_FRAME_FROM_BUFFER(consumed + HeaderSize, payloadLen);

                    // Re-integrate the FragmentAssembler eviction logic.
                    if ((++_packetCount & (FragmentAssembler.EvictInterval - 1)) == 0)
                    {
                        FragmentAssembler? fragmentAssembler = _fragmentAssembler;
                        int evicted = fragmentAssembler?.EvictExpired() ?? 0;
                        if (evicted > 0)
                        {
                            Interlocked.Add(ref _openFragmentStreams, -evicted);

                            _sender?.ThrottledWarn(
                                _logger, "socket.receive.evicted_fragments",
                                $"evicted {evicted} stale fragment stream(s) ep={_sender.NetworkEndpoint.Address}");
                        }
                    }

                    consumed += size;
                    parsedAtLeastOne = true;
                }

                // Step 2: Compact the buffer.
                if (consumed > 0)
                {
                    int remaining = _bufferDataLength - consumed;
                    if (remaining > 0)
                    {
                        Buffer.BlockCopy(_buffer!, consumed, _buffer!, 0, remaining);
                    }
                    _bufferDataLength = remaining;
                }

                // Step 3: If we didn't parse any frames in this iteration, OR we still have a partial frame,
                // we MUST await more data from the socket to avoid a tight-loop spin.
                if (!parsedAtLeastOne || _bufferDataLength < HeaderSize)
                {
                    await this.RECEIVE_OPPORTUNISTIC_ASYNC(token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (IS_BENIGN_DISCONNECT(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger?.Trace(
                    $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                    $"ended (peer closed/shutdown) ep={_sender?.NetworkEndpoint.Address}");
            }
        }
        catch (OperationCanceledException)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.Trace(
                    $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_ASYNC)}] " +
                    $"cancelled ep={_sender?.NetworkEndpoint.Address}");
            }
        }
        catch (Exception ex)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                Exception e = (ex as AggregateException)?.Flatten() ?? ex;

                _sender.ThrottledError(
                    _logger, "socket.receive.faulted",
                    $"faulted ep={_sender.NetworkEndpoint.Address}", e);
            }

        }
        finally
        {
            this.CANCEL_RECEIVE_ONCE();
            this.INVOKE_CLOSE_ONCE();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask RECEIVE_OPPORTUNISTIC_ASYNC(CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(token);
        }

        int freeSpace = _buffer!.Length - _bufferDataLength;
        if (freeSpace == 0)
        {
            // If the buffer is full but we haven't parsed a complete frame, it means a single 
            // frame has exceeded our buffer capacity (MaxChunkSize * 2). 
            // Since the system is configured to never send frames > 1400 bytes, this is a protocol violation.
            return ValueTask.FromException(NetworkErrors.MessageSize);
        }

        ValueTask<int> vt = _recvCtx.ReceiveAsync(_socket, _buffer, _bufferDataLength, freeSpace);

        if (vt.IsCompletedSuccessfully)
        {
            int n = vt.Result;
            if (n == 0)
            {
                return ValueTask.FromException(NetworkErrors.ConnectionReset);
            }

            _bufferDataLength += n;
            return default;
        }

        return AWAIT_RECEIVE(this, vt);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AWAIT_RECEIVE(SocketConnection self, ValueTask<int> vt)
        {
            int n = await vt.ConfigureAwait(false);
            if (n == 0)
            {
                throw NetworkErrors.ConnectionReset;
            }

            self._bufferDataLength += n;
        }
    }

    /// <summary>
    /// Processes a single frame by copying it into a new BufferLease.
    /// This allows the receive loop to continue without waiting for the pipeline.
    /// </summary>
    private void PROCESS_FRAME_FROM_BUFFER(int offset, int payloadLen)
    {
        // Layer 1 per-connection throttle check.
        int pending = Interlocked.Increment(ref _pendingProcessCallbacks);
        if (pending > s_opts.MaxPerConnectionPendingPackets)
        {
            Interlocked.Decrement(ref _pendingProcessCallbacks);

            _sender?.ThrottledWarn(
                _logger, "socket.receive.throttle",
                $"throttle triggered — packet dropped ep={_endpointString}");

            return;
        }

        // Copy frame data into a new lease.
        BufferLease lease = BufferLease.CopyFrom(MemoryExtensions.AsSpan(_buffer, offset, payloadLen));
        lease.IsReliable = true;

        this.LastPingTime = Clock.UnixMillisecondsNow();
        ConnectionEventArgs? args = (_sender as Connection)?.AcquireEventArgs() ?? s_pool.Get<ConnectionEventArgs>();
        ReadOnlySpan<byte> payloadSpan = lease.Span;

        // 2. Fragment Assembly Check.
        // A FragmentHeader is 7 bytes. If it's a fragment, we handle it separately.
        if (FragmentAssembler.IsFragmentedFrame(payloadSpan, out FragmentHeader header))
        {
            this.HANDLE_FRAGMENTED_FRAME(lease, args, header);
        }
        else
        {
            // 3. Regular Frame Path.
            // Safety: The application protocol (FramePipeline) requires a 10-byte header.
            // If the payload is too small, it's a malformed packet that would cause OOB reads.
            if (payloadLen < PacketConstants.HeaderSize)
            {
#if DEBUG
                _logger?.Warn(
                    $"[NW.{nameof(SocketConnection)}] malformed-payload " +
                    $"length={payloadLen} (too small for protocol header) ep={_endpointString}");
#endif
                Interlocked.Decrement(ref _pendingProcessCallbacks);
                args.Dispose();
                lease.Dispose();
                return;
            }

            args.Initialize(lease, _cachedArgs.Connection);

            if (!AsyncCallback.Invoke(_callbackProcess, _sender, args, releasePendingPacketOnCompletion: true))
            {
                Interlocked.Decrement(ref _pendingProcessCallbacks);
                args.Dispose();
                lease.Dispose();   // ← was missing: return buffer to pool when queue is full
            }
        }

#if DEBUG
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            _logger.Debug(
                $"[NW.{nameof(SocketConnection)}] handoff-to-cache " +
                $"payload={payloadLen} pending={pending} ep={_endpointString}");
        }
#endif
    }

    /// <summary>
    /// Helper to handle fragmented frames, extracted for clarity.
    /// </summary>
    private void HANDLE_FRAGMENTED_FRAME(BufferLease lease, ConnectionEventArgs args, FragmentHeader header)
    {
        try
        {
            FragmentAssembler fragmentAssembler = this.GET_OR_CREATE_FRAGMENT_ASSEMBLER();
            ReadOnlySpan<byte> chunkBody = lease.Span[FragmentHeader.WireSize..];

            if (header.ChunkIndex == 0)
            {
                int openStreams = Interlocked.Increment(ref _openFragmentStreams);
                if (openStreams > s_opts.MaxPerConnectionOpenFragmentStreams)
                {
                    Interlocked.Decrement(ref _openFragmentStreams);
                    Interlocked.Decrement(ref _pendingProcessCallbacks);
                    args.Dispose();

#if DEBUG
                    if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    {
                        _logger.Debug($"[NW.{nameof(SocketConnection)}] fragment-limit open={openStreams} ep={_endpointString}");
                    }
#endif
                    return;
                }
            }

#if DEBUG
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.Debug(
                    $"[NW.{nameof(SocketConnection)}] recv-frag stream={header.StreamId} chunk={header.ChunkIndex}/{header.TotalChunks} " +
                    $"last={header.IsLast} ep={_endpointString}");
            }
#endif

            FragmentAssemblyResult? assembled = fragmentAssembler.Add(header, chunkBody, out bool streamEvicted);
            if (assembled is not null)
            {
                BufferLease assembledLease = assembled.Value.Lease;
                assembledLease.IsReliable = true;
                assembledLease.Retain();
                args.Initialize(assembledLease, _cachedArgs.Connection);

                if (!AsyncCallback.Invoke(_callbackProcess, _sender, args, releasePendingPacketOnCompletion: true))
                {
                    Interlocked.Decrement(ref _pendingProcessCallbacks);
                    Interlocked.Decrement(ref _openFragmentStreams);
                    args.Dispose();
                    assembledLease.Dispose();  // ← was missing: must dispose on both paths
                }
                else
                {
#if DEBUG
                    _logger?.Debug($"[NW.{nameof(SocketConnection)}] assembled stream={header.StreamId} ep={_endpointString}");
#endif
                    Interlocked.Decrement(ref _openFragmentStreams);
                    assembledLease.Dispose();
                }
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
        }
        finally
        {
            // ALWAYS dispose the input lease because fragmentAssembler.Add COPIES the data.
            lease.Dispose();
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
            // 1. Signal cancellation so the receive loop exits cleanly and stops
            //    scheduling any more receives.
            this.CANCEL_RECEIVE_ONCE();

            // 2. Shutdown and close the socket. This forces any in-flight SAEA
            //    receive to complete or abort, which lets the pooled receive
            //    context observe an idle state and become returnable.
            try
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch { /* ignore */ }

            try { _socket.Close(); } catch { /* ignore */ }

            Task? receiveLoopTask = _receiveLoopTask;
            if (receiveLoopTask is not null)
            {
                _receiveLoopTask = null;

                // Wait for the loop to exit. Since we closed the socket above, 
                // the loop should exit almost immediately. This ensures that 
                // any pending AWAIT_RECEIVE has finished and released its 
                // references to this connection.
                try { receiveLoopTask.GetAwaiter().GetResult(); } catch { /* ignore */ }
            }

            // 3. Return the pooled receive context only after the socket can no
            //    longer use it.
            if (_recvCtx is not null)
            {
                // Always dispose/return context. PooledSocketReceiveContext.Dispose() 
                // contains defensive wait logic to ensure kernel marks SAEA as idle.
                // Not returning it here caused the approx 524 object leak identified in stress tests.
                _recvCtx.Dispose();
                s_pool.Return(_recvCtx);

                _recvCtx = null!;
            }

            // 6. Fire the close callback after the socket and buffers are already
            //    out of circulation.
            this.INVOKE_CLOSE_ONCE();

            // 7. Dispose remaining resources.
            _socket.Dispose();
            Interlocked.Exchange(ref _fragmentAssembler, null)?.Dispose();
        }

        // 4. Return the receive buffer. Interlocked.Exchange prevents double-
        //    return if Dispose races with the receive loop cleanup.
        //    IMPORTANT: We move this OUTSIDE the 'if (disposing)' block to ensure 
        //    the pooled buffer is returned even if the connection object is leaked 
        //    and GC'd without an explicit Dispose() call.
        byte[]? bufToReturn = Interlocked.Exchange(ref _buffer, null!);
        if (bufToReturn is not null)
        {
            BufferLease.ByteArrayPool.Return(bufToReturn);
        }

#if DEBUG
        _logger?.Trace(
            $"[NW.{nameof(SocketConnection)}:{nameof(Dispose)}] " +
            $"disposed ep={_endpointString}");
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

        if (!AsyncCallback.InvokeHighPriority(_callbackClose, _sender, args))
        {
            args.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CANCEL_RECEIVE_ONCE()
    {
        if (Interlocked.Exchange(ref _cancelSignaled, 1) != 0)
        {
            return;
        }
    }

    private void THROW_IF_NOT_CONFIGURED()
    {
        if (_sender is null || _cachedArgs is null)
        {
            throw new InternalErrorException("SetCallback must be called before use");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FragmentAssembler GET_OR_CREATE_FRAGMENT_ASSEMBLER()
    {
        FragmentAssembler? assembler = _fragmentAssembler;
        if (assembler is not null)
        {
            return assembler;
        }

        assembler = new FragmentAssembler();
        FragmentAssembler? existing = Interlocked.CompareExchange(ref _fragmentAssembler, assembler, null);
        if (existing is not null)
        {
            assembler.Dispose();
            return existing;
        }

        return assembler;
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
