// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Configuration;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
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
    Func<System.Net.Sockets.Socket> getSocket,
    TransportOptions options,
    Action<BufferLease> onMessage,
    Action<Exception> onError,
    Action<int> reportBytesReceived)
{
    private readonly TransportOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    private readonly Func<System.Net.Sockets.Socket> _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));

    private readonly Action<Exception> _onError = onError ?? throw new ArgumentNullException(nameof(onError));

    /// <summary>
    /// Concrete type BufferLease — HandleReceiveMessage should call CopyFrom(lease.Span)
    /// without casts.
    /// </summary>
    private readonly Action<BufferLease> _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));

    private readonly Action<int> _reportBytesReceived = reportBytesReceived ?? throw new ArgumentNullException(nameof(reportBytesReceived));

    /// <summary>
    /// Main receive loop. Reads framed messages with a 2-byte little-endian total-length header.
    /// On each full frame, creates a BufferLease (ownership transferred to _onMessage).
    /// </summary>
    /// <param name="token"></param>
    /// <exception cref="System.Net.Sockets.SocketException"></exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "<Pending>")]
    public async System.Threading.Tasks.Task ReceiveLoopAsync(
        System.Threading.CancellationToken token)
    {
        System.Net.Sockets.Socket s;
        try
        {
            s = _getSocket();
            TcpSessionBase.Logging?.Trace($"[SDK.{nameof(FRAME_READER)}] receive-loop starting; endpoint={FORMAT_ENDPOINT(s)}");
        }
        catch (Exception ex)
        {
            TcpSessionBase.Logging?.Error($"[SDK.{nameof(FRAME_READER)}] receive-start-error {ex.Message}", ex);
            _onError(ex);
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1) Read 2-byte length header
                byte[] headerBuffer =
                    System.Buffers.ArrayPool<byte>.Shared.Rent(TcpSession.HeaderSize);
                try
                {
                    Memory<byte> headerMemory = new(
                        headerBuffer, 0, TcpSession.HeaderSize);

                    await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);

                    ushort totalLen =
                        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                            headerMemory.Span);

                    TcpSessionBase.Logging?.Debug($"[SDK.{nameof(FRAME_READER)}] header-read totalLen={totalLen} endpoint={FORMAT_ENDPOINT(s)}");

                    if (totalLen < TcpSession.HeaderSize || totalLen > _options.MaxPacketSize)
                    {
                        TcpSessionBase.Logging?.Warn(
                            $"[SDK.{nameof(FRAME_READER)}] invalid-packet-size totalLen={totalLen} " +
                            $"headerSize={TcpSession.HeaderSize} max={_options.MaxPacketSize} " +
                            $"endpoint={FORMAT_ENDPOINT(s)}");
                        throw new System.Net.Sockets.SocketException(
                            (int)System.Net.Sockets.SocketError.ProtocolNotSupported);
                    }

                    int payloadLen = totalLen - TcpSession.HeaderSize;

                    // 2) Rent buffer for full frame and read payload
                    byte[] rented = BufferLease.ByteArrayPool.Rent(totalLen);
                    const bool ownershipTransferred = false;

                    try
                    {
                        TcpSessionBase.Logging?.Trace(
                            $"[SDK.{nameof(FRAME_READER)}] rented-buffer size={rented.Length} " +
                            $"frameTotal={totalLen} payload={payloadLen} endpoint={FORMAT_ENDPOINT(s)}");

                        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                            MemoryExtensions.AsSpan(rented, 0, TcpSession.HeaderSize),
                            totalLen);

                        if (payloadLen > 0)
                        {
                            await RECEIVE_EXACTLY_ASYNC(
                                s,
                                MemoryExtensions.AsMemory(
                                    rented, TcpSession.HeaderSize, payloadLen),
                                token).ConfigureAwait(false);
                        }

                        // Best-effort telemetry — never throw
                        try
                        {
                            _reportBytesReceived(totalLen);
                        }
                        catch (Exception teleEx)
                        {
                            TcpSessionBase.Logging?.Warn(
                                $"[SDK.{nameof(FRAME_READER)}] report-bytes-received failed: {teleEx.Message}",
                                teleEx);
                        }

                        // 3) Wrap into BufferLease — ownership transferred to _onMessage.
                        // CONTRACT: HandleReceiveMessage is the SOLE OWNER and must Dispose the lease.
                        BufferLease lease = BufferLease.TakeOwnership(
                            rented, TcpSession.HeaderSize, payloadLen);

                        // Xử lý DECODE trực tiếp
                        PacketFlags flags = lease.Span.ReadFlagsLE();

                        // Giải mã nếu cần
                        if (flags.HasFlag(PacketFlags.ENCRYPTED))
                        {
                            BufferLease decryptedLease = BufferLease.Rent(FrameTransformer.GetPlaintextLength(lease.Span));
                            if (!FrameTransformer.TryDecrypt(lease, decryptedLease, _options.Secret))
                            {
                                decryptedLease.Dispose();
                                lease.Dispose();
                                throw new Exception("Failed to decrypt");
                            }
                            decryptedLease.Span.WriteFlagsLE(decryptedLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.ENCRYPTED));
                            lease.Dispose();
                            lease = decryptedLease;
                            flags = lease.Span.ReadFlagsLE(); // Update flags
                        }

                        // Giải nén nếu cần
                        if (flags.HasFlag(PacketFlags.COMPRESSED))
                        {
                            BufferLease decomLease = BufferLease.Rent(FrameTransformer.GetDecompressedLength(lease.Span));
                            if (!FrameTransformer.TryDecompress(lease, decomLease))
                            {
                                decomLease.Dispose();
                                lease.Dispose();
                                throw new Exception("Failed to decompress");
                            }
                            decomLease.Span.WriteFlagsLE(decomLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.COMPRESSED));
                            lease.Dispose();
                            lease = decomLease;
                            flags = lease.Span.ReadFlagsLE(); // Update flags
                        }

                        // 4) Deliver — catch exceptions from handler to protect loop.
                        // DO NOT dispose lease here — handler is responsible.
                        try
                        {
                            _onMessage(lease);

                            TcpSessionBase.Logging?.Trace(
                                $"[SDK.{nameof(FRAME_READER)}] handler-invoked-success payload={payloadLen} endpoint={FORMAT_ENDPOINT(s)}");
                        }
                        catch (Exception handlerEx)
                        {
                            // Handler faulted after ownershipTransferred = true.
                            // HandleReceiveMessage should finally { lease.Dispose(); }
                            // so buffer likely already returned — log and continue.
                            TcpSessionBase.Logging?.Error(
                                $"[SDK.{nameof(FRAME_READER)}] handler-faulted—loop continues. msg={handlerEx.Message}",
                                handlerEx);
                        }
                    }
                    catch (Exception)
                    {
                        // Only return raw buffer if lease was NOT created (ownershipTransferred == false).
                        if (!ownershipTransferred)
                        {
                            try
                            {
                                BufferLease.ByteArrayPool.Return(rented);
                                TcpSessionBase.Logging?.Trace($"[SDK.{nameof(FRAME_READER)}] returned-rented-buffer size={rented?.Length} endpoint={FORMAT_ENDPOINT(s)}");
                            }
                            catch (Exception returnEx)
                            {
                                TcpSessionBase.Logging?.Warn($"[SDK.{nameof(FRAME_READER)}] failed-returning-buffer: {returnEx.Message}", returnEx);
                            }
                        }

                        // Re-throw to outer catch which will call _onError.
                        throw;
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(headerBuffer);
                }
            }

            // Normal cancellation: log graceful stop
            TcpSessionBase.Logging?.Trace($"[SDK.{nameof(FRAME_READER)}] receive-loop ending normally endpoint={FORMAT_ENDPOINT(s)}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown — not an error.
            TcpSessionBase.Logging?.Trace($"[SDK.{nameof(FRAME_READER)}] receive-loop cancelled endpoint={FORMAT_ENDPOINT(s)}");
        }
        catch (Exception ex)
        {
            TcpSessionBase.Logging?.Error($"[SDK.{nameof(FRAME_READER)}:{nameof(ReceiveLoopAsync)}] faulted msg={ex.Message} endpoint={FORMAT_ENDPOINT(s)}", ex);

            try { _onError(ex); } catch { /* swallow to avoid crash */ }
        }
    }

    private static async System.Threading.Tasks.Task RECEIVE_EXACTLY_ASYNC(
        System.Net.Sockets.Socket s,
        Memory<byte> dst,
        System.Threading.CancellationToken token)
    {
        int read = 0;
        while (read < dst.Length)
        {
            int n = await s.ReceiveAsync(
                dst[read..],
                System.Net.Sockets.SocketFlags.None,
                token).ConfigureAwait(false);

            if (n == 0)
            {
                throw new System.Net.Sockets.SocketException(
                    (int)System.Net.Sockets.SocketError.ConnectionReset);
            }

            read += n;
        }
    }

    /// <summary>
    /// Safe formatting when socket may be null or disposed in some error paths.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    [System.Diagnostics.DebuggerStepThrough]
    private static string FORMAT_ENDPOINT(System.Net.Sockets.Socket? s)
    {
        if (s is null)
        {
            return "<null-socket>";
        }

        try { return s.RemoteEndPoint?.ToString() ?? "<unknown>"; }
        catch (ObjectDisposedException) { return "<disposed>"; }
        catch { return "<unknown>"; }
    }
}
