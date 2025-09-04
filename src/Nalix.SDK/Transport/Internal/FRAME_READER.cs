// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Configuration;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport.Internal;

/// <inheritdoc/>
/// <remarks>
/// Ownership contract (updated):
///   FRAME_READER creates a <see cref="BufferLease"/> via <see cref="BufferLease.TakeOwnership"/>
///   and passes it to <paramref name="onMessage"/> (= <c>HandleReceiveMessage</c> in ReliableClient).
///   <c>HandleReceiveMessage</c> is the SOLE owner of the lease — it creates per-subscriber copies
///   via <see cref="BufferLease.CopyFrom"/> and disposes the original lease in its own finally block.
///   FRAME_READER never touches the lease after calling _onMessage.
/// </remarks>
internal sealed class FRAME_READER(
    System.Func<System.Net.Sockets.Socket> getSocket,
    TransportOptions options,
    System.Action<BufferLease> onMessage,        // ← concrete BufferLease
    System.Action<System.Exception> onError,
    System.Action<System.Int32> reportBytesReceived)
{
    private readonly TransportOptions _options = options ?? throw new System.ArgumentNullException(nameof(options));

    private readonly System.Func<System.Net.Sockets.Socket> _getSocket = getSocket ?? throw new System.ArgumentNullException(nameof(getSocket));

    private readonly System.Action<System.Exception> _onError = onError ?? throw new System.ArgumentNullException(nameof(onError));

    // Concrete type BufferLease — HandleReceiveMessage should call CopyFrom(lease.Span)
    // without casts.
    private readonly System.Action<BufferLease> _onMessage = onMessage ?? throw new System.ArgumentNullException(nameof(onMessage));

    private readonly System.Action<System.Int32> _reportBytesReceived = reportBytesReceived ?? throw new System.ArgumentNullException(nameof(reportBytesReceived));

    /// <summary>
    /// Main receive loop. Reads framed messages with a 2-byte little-endian total-length header.
    /// On each full frame, creates a BufferLease (ownership transferred to _onMessage).
    /// </summary>
    public async System.Threading.Tasks.Task ReceiveLoopAsync(
        System.Threading.CancellationToken token)
    {
        System.Net.Sockets.Socket s;
        try
        {
            s = _getSocket();
            BaseTcpSession.Logging?.Meta($"[SDK.{nameof(FRAME_READER)}] receive-loop starting; endpoint={FORMAT_ENDPOINT(s)}");
        }
        catch (System.Exception ex)
        {
            BaseTcpSession.Logging?.Error($"[SDK.{nameof(FRAME_READER)}] receive-start-error {ex.Message}", ex);
            _onError(ex);
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1) Read 2-byte length header
                System.Byte[] headerBuffer =
                    System.Buffers.ArrayPool<System.Byte>.Shared.Rent(TcpSession.HeaderSize);
                try
                {
                    var headerMemory = new System.Memory<System.Byte>(
                        headerBuffer, 0, TcpSession.HeaderSize);

                    await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);

                    System.UInt16 totalLen =
                        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                            headerMemory.Span);

                    BaseTcpSession.Logging?.Debug($"[SDK.{nameof(FRAME_READER)}] header-read totalLen={totalLen} endpoint={FORMAT_ENDPOINT(s)}");

                    if (totalLen < TcpSession.HeaderSize || totalLen > _options.MaxPacketSize)
                    {
                        BaseTcpSession.Logging?.Warn(
                            $"[SDK.{nameof(FRAME_READER)}] invalid-packet-size totalLen={totalLen} " +
                            $"headerSize={TcpSession.HeaderSize} max={_options.MaxPacketSize} " +
                            $"endpoint={FORMAT_ENDPOINT(s)}");
                        throw new System.Net.Sockets.SocketException(
                            (System.Int32)System.Net.Sockets.SocketError.ProtocolNotSupported);
                    }

                    System.Int32 payloadLen = totalLen - TcpSession.HeaderSize;

                    // 2) Rent buffer for full frame and read payload
                    System.Byte[] rented = BufferLease.ByteArrayPool.Rent(totalLen);
                    System.Boolean ownershipTransferred = false;
                    try
                    {
                        BaseTcpSession.Logging?.Trace(
                            $"[SDK.{nameof(FRAME_READER)}] rented-buffer size={rented.Length} " +
                            $"frameTotal={totalLen} payload={payloadLen} endpoint={FORMAT_ENDPOINT(s)}");

                        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                            System.MemoryExtensions.AsSpan(rented, 0, TcpSession.HeaderSize),
                            totalLen);

                        if (payloadLen > 0)
                        {
                            await RECEIVE_EXACTLY_ASYNC(
                                s,
                                System.MemoryExtensions.AsMemory(
                                    rented, TcpSession.HeaderSize, payloadLen),
                                token).ConfigureAwait(false);
                        }

                        // Best-effort telemetry — never throw
                        try
                        {
                            _reportBytesReceived(totalLen);
                        }
                        catch (System.Exception teleEx)
                        {
                            BaseTcpSession.Logging?.Warn(
                                $"[SDK.{nameof(FRAME_READER)}] report-bytes-received failed: {teleEx.Message}",
                                teleEx);
                        }

                        // 3) Wrap into BufferLease — ownership transferred to _onMessage.
                        // CONTRACT: HandleReceiveMessage is the SOLE OWNER and must Dispose the lease.
                        BufferLease lease = BufferLease.TakeOwnership(
                            rented, TcpSession.HeaderSize, payloadLen);
                        ownershipTransferred = true;

                        BaseTcpSession.Logging?.Debug(
                            $"[SDK.{nameof(FRAME_READER)}] delivering-lease payload={payloadLen} endpoint={FORMAT_ENDPOINT(s)}");

                        // 4) Deliver — catch exceptions from handler to protect loop.
                        // DO NOT dispose lease here — handler is responsible.
                        try
                        {
                            _onMessage(lease);
                            BaseTcpSession.Logging?.Trace(
                                $"[SDK.{nameof(FRAME_READER)}] handler-invoked-success payload={payloadLen} endpoint={FORMAT_ENDPOINT(s)}");
                        }
                        catch (System.Exception handlerEx)
                        {
                            // Handler faulted after ownershipTransferred = true.
                            // HandleReceiveMessage should finally { lease.Dispose(); }
                            // so buffer likely already returned — log and continue.
                            BaseTcpSession.Logging?.Error(
                                $"[SDK.{nameof(FRAME_READER)}] handler-faulted—loop continues. msg={handlerEx.Message}",
                                handlerEx);
                        }
                    }
                    catch (System.Exception)
                    {
                        // Only return raw buffer if lease was NOT created (ownershipTransferred == false).
                        if (!ownershipTransferred)
                        {
                            try
                            {
                                BufferLease.ByteArrayPool.Return(rented);
                                BaseTcpSession.Logging?.Trace($"[SDK.{nameof(FRAME_READER)}] returned-rented-buffer size={rented?.Length} endpoint={FORMAT_ENDPOINT(s)}");
                            }
                            catch (System.Exception returnEx)
                            {
                                BaseTcpSession.Logging?.Warn($"[SDK.{nameof(FRAME_READER)}] failed-returning-buffer: {returnEx.Message}", returnEx);
                            }
                        }

                        // Re-throw to outer catch which will call _onError.
                        throw;
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<System.Byte>.Shared.Return(headerBuffer);
                }
            }

            // Normal cancellation: log graceful stop
            BaseTcpSession.Logging?.Meta($"[SDK.{nameof(FRAME_READER)}] receive-loop ending normally endpoint={FORMAT_ENDPOINT(s)}");
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown — not an error.
            BaseTcpSession.Logging?.Trace($"[SDK.{nameof(FRAME_READER)}] receive-loop cancelled endpoint={FORMAT_ENDPOINT(s)}");
        }
        catch (System.Exception ex)
        {
            BaseTcpSession.Logging?.Error($"[SDK.{nameof(FRAME_READER)}:{nameof(ReceiveLoopAsync)}] faulted msg={ex.Message} endpoint={FORMAT_ENDPOINT(s)}", ex);

            try { _onError(ex); } catch { /* swallow to avoid crash */ }
        }
    }

    private static async System.Threading.Tasks.Task RECEIVE_EXACTLY_ASYNC(
        System.Net.Sockets.Socket s,
        System.Memory<System.Byte> dst,
        System.Threading.CancellationToken token)
    {
        System.Int32 read = 0;
        while (read < dst.Length)
        {
            System.Int32 n = await s.ReceiveAsync(
                dst[read..],
                System.Net.Sockets.SocketFlags.None,
                token).ConfigureAwait(false);

            if (n == 0)
            {
                throw new System.Net.Sockets.SocketException(
                    (System.Int32)System.Net.Sockets.SocketError.ConnectionReset);
            }

            read += n;
        }
    }

    // Safe formatting when socket may be null or disposed in some error paths.
    [System.Diagnostics.DebuggerStepThrough]
    private static System.String FORMAT_ENDPOINT(System.Net.Sockets.Socket? s)
    {
        if (s is null)
        {
            return "<null-socket>";
        }

        try { return s.RemoteEndPoint?.ToString() ?? "<unknown>"; }
        catch (System.ObjectDisposedException) { return "<disposed>"; }
        catch { return "<unknown>"; }
    }
}