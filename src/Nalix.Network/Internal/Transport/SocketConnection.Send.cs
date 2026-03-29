// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;

namespace Nalix.Network.Internal.Transport;

internal sealed partial class SocketConnection
{
    /// <summary>
    /// Sends data synchronously.
    /// Small packets (≤ <see cref="PacketConstants.StackAllocLimit"/>) are framed on the
    /// stack; larger ones use a pooled heap buffer.
    /// </summary>
    /// <param name="data"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Send(ReadOnlySpan<byte> data)
    {
        this.THROW_IF_NOT_CONFIGURED();

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, nameof(SocketConnection));

        if (data.IsEmpty)
        {
            throw new ArgumentException("Data must not be empty.", nameof(data));
        }

        if (data.Length >= s_fragmentOptions.ChunkThreshold)
        {
            this.SEND_FRAGMENTED(data);
            return;
        }

        int totalLength = data.Length + HeaderSize;
        if (totalLength > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                totalLength,
                $"Non-fragmented frame size must not exceed {ushort.MaxValue} bytes.");
        }

        // ── Fast path: stack-allocate frame for small packets ─────────────
        if (data.Length <= PacketConstants.StackAllocLimit)
        {
            try
            {
#if DEBUG
                s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                $"stackalloc len={data.Length} ep={_socket.RemoteEndPoint}");
#endif
                Span<byte> frameS = stackalloc byte[totalLength];
                WRITE_FRAME_HEADER(frameS, (ushort)totalLength, data);

#if DEBUG
                if (s_logger is not null)
                {
                    Span<byte> payloadSpan = frameS.Slice(HeaderSize, data.Length);
                    s_logger.Debug($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                   $"sending frame totalLen={totalLength} payload={FORMAT_FRAME_FOR_LOG(payloadSpan)} ep={_socket.RemoteEndPoint}");
                }
#endif

                int sent = 0;
                while (sent < frameS.Length)
                {
                    int n = _socket.Send(frameS[sent..]);
                    if (n == 0)
                    {
#if DEBUG
                        s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                        $"stackalloc peer-closed ep={_socket.RemoteEndPoint}");
#endif
                        this.CANCEL_RECEIVE_ONCE();
                        this.INVOKE_CLOSE_ONCE();
                        throw new InvalidOperationException("The socket closed while sending.");
                    }
                    sent += n;
                }

                ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
                args.Initialize(_cachedArgs.Connection);

                AsyncCallback.Invoke(_callbackPost, _sender, args);
                return;
            }
            catch (Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                $"stackalloc-error ep={_socket.RemoteEndPoint}", ex);
                throw;
            }
        }

        // ── Slow path: pooled heap buffer ──────────────────────────────────
        byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);
        try
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                            $"pooled len={data.Length} ep={_socket.RemoteEndPoint}");
#endif
            BinaryPrimitives.WriteUInt16LittleEndian(MemoryExtensions.AsSpan(heapBuf), (ushort)totalLength);
            data.CopyTo(MemoryExtensions.AsSpan(heapBuf, HeaderSize));

#if DEBUG
            if (s_logger is not null)
            {
                Span<byte> payloadSpan = MemoryExtensions.AsSpan(heapBuf, HeaderSize, data.Length);
                s_logger.Debug($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                               $"sending frame totalLen={totalLength} payload={FORMAT_FRAME_FOR_LOG(payloadSpan)} " +
                               $"ep={_socket.RemoteEndPoint}");
            }
#endif

            int sent = 0;
            while (sent < totalLength)
            {
                int n = _socket.Send(heapBuf, sent, totalLength - sent,
                                              SocketFlags.None);
                if (n == 0)
                {
#if DEBUG
                    s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                    $"pooled peer-closed ep={_socket.RemoteEndPoint}");
#endif
                    this.CANCEL_RECEIVE_ONCE();
                    this.INVOKE_CLOSE_ONCE();
                    throw new InvalidOperationException("The socket closed while sending.");
                }
                sent += n;
            }

            this.InvokePostCallback();
            return;
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                            $"pooled-error ep={_socket.RemoteEndPoint}", ex);
            throw;
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
        }
    }

    /// <summary>
    /// Sends data asynchronously. Uses a pooled heap buffer for framing.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns><see langword="true"/> if the data was sent successfully.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        this.THROW_IF_NOT_CONFIGURED();

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, nameof(SocketConnection));

        if (data.IsEmpty)
        {
            throw new ArgumentException("Data must not be empty.", nameof(data));
        }

        if (data.Length >= s_fragmentOptions.ChunkThreshold)
        {
            await this.SEND_FRAGMENTED_ASYNC(data, cancellationToken).ConfigureAwait(false);
            return;
        }

        int totalLength = data.Length + HeaderSize;
        if (totalLength > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                totalLength,
                $"Non-fragmented frame size must not exceed {ushort.MaxValue} bytes.");
        }

        byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);

        try
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(SendAsync)}] " +
                            $"len={data.Length} ep={_socket.RemoteEndPoint}");
#endif
            WRITE_FRAME_HEADER(MemoryExtensions.AsSpan(heapBuf), (ushort)totalLength, data.Span);

#if DEBUG
            if (s_logger is not null)
            {
                ReadOnlySpan<byte> payloadSpan = data.Span;
                s_logger.Debug($"[NW.{nameof(SocketConnection)}:{nameof(SendAsync)}] " +
                               $"sending async frame totalLen={totalLength} payload={FORMAT_FRAME_FOR_LOG(payloadSpan)} " +
                               $"ep={_socket.RemoteEndPoint}");
            }
#endif

            int sent = 0;
            while (sent < totalLength)
            {
                int n = await _socket.SendAsync(MemoryExtensions
                                     .AsMemory(heapBuf, sent, totalLength - sent), SocketFlags.None, cancellationToken)
                                     .ConfigureAwait(false);

                if (n == 0)
                {
#if DEBUG
                    s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(SendAsync)}] " +
                                    $"peer-closed ep={_socket.RemoteEndPoint}");
#endif
                    this.CANCEL_RECEIVE_ONCE();
                    this.INVOKE_CLOSE_ONCE();
                    throw new InvalidOperationException("The socket closed while sending.");
                }
                sent += n;
            }

            this.InvokePostCallback();
            return;
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(SocketConnection)}:{nameof(SendAsync)}] " +
                            $"error ep={_socket.RemoteEndPoint}", ex);
            throw;
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
        }
    }

    #region Fragmented Send Helpers

    /// <summary>
    /// Gửi payload lớn bằng cách tự động fragment.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2014:Do not use stackalloc in loops", Justification = "<Pending>")]
    private void SEND_FRAGMENTED(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > s_fragmentOptions.MaxPayloadSize)
        {
            throw new ArgumentOutOfRangeException(nameof(payload),
                $"Payload exceeds maximum allowed size {s_fragmentOptions.MaxPayloadSize}");
        }

        ushort streamId = FragmentStreamId.Next();
        int chunkBodySize = s_fragmentOptions.ChunkBodySize;
        int totalChunks = (payload.Length + chunkBodySize - 1) / chunkBodySize;
        if (totalChunks > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"Fragmented payload requires {totalChunks} chunks, which exceeds the {ushort.MaxValue}-chunk wire header limit.");
        }

        Span<byte> headerBuffer = stackalloc byte[FragmentHeader.WireSize];

        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * chunkBodySize;
            int remaining = payload.Length - offset;
            int thisChunkSize = Math.Min(remaining, chunkBodySize);

            bool isLast = i == totalChunks - 1;

            FragmentHeader fragHeader = new(
                streamId: streamId,
                chunkIndex: (ushort)i,
                totalChunks: (ushort)totalChunks,
                isLast: isLast);

            fragHeader.WriteTo(headerBuffer);

            // Calculate the total frame size for this segment
            int framePayloadSize = FragmentHeader.WireSize + thisChunkSize;

            // + 2 byte length
            int totalFrameSize = HeaderSize + framePayloadSize;

            if (totalFrameSize > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Fragmented frame size {totalFrameSize} exceeds the {ushort.MaxValue}-byte wire header limit.");
            }

            // Fast path: stackalloc if small
            if (totalFrameSize <= PacketConstants.StackAllocLimit)
            {
                Span<byte> frame = stackalloc byte[totalFrameSize];

                // Write outer frame length
                BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)totalFrameSize);

                // Write FragmentHeader + Magic + body
                headerBuffer.CopyTo(frame[HeaderSize..]);
                payload.Slice(offset, thisChunkSize).CopyTo(frame[(HeaderSize + FragmentHeader.WireSize)..]);

                SEND_RAW_FRAME(frame);
            }
            else
            {
                // Slow path: pooled buffer
                byte[] rented = BufferLease.ByteArrayPool.Rent(totalFrameSize);
                try
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(MemoryExtensions.AsSpan(rented), (ushort)totalFrameSize);
                    headerBuffer.CopyTo(MemoryExtensions.AsSpan(rented, HeaderSize));
                    payload.Slice(offset, thisChunkSize).CopyTo(MemoryExtensions.AsSpan(rented, HeaderSize + FragmentHeader.WireSize));

                    SEND_RAW_FRAME(rented.AsSpan(0, totalFrameSize));
                }
                finally
                {
                    BufferLease.ByteArrayPool.Return(rented);
                }
            }
        }

        this.InvokePostCallback();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SEND_RAW_FRAME(ReadOnlySpan<byte> frame)
        {
            int sent = 0;
            while (sent < frame.Length)
            {
                int n = _socket.Send(frame[sent..]);
                if (n == 0)
                {
                    this.CANCEL_RECEIVE_ONCE();
                    this.INVOKE_CLOSE_ONCE();
                    throw new InvalidOperationException("The socket closed while sending.");
                }
                sent += n;
            }
        }
    }

    private async Task SEND_FRAGMENTED_ASYNC(ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        if (payload.Length > s_fragmentOptions.MaxPayloadSize)
        {
            throw new ArgumentOutOfRangeException(nameof(payload),
                $"Payload exceeds maximum allowed size {s_fragmentOptions.MaxPayloadSize}");
        }

        ushort streamId = FragmentStreamId.Next();
        int chunkBodySize = s_fragmentOptions.ChunkBodySize;
        int totalChunks = (payload.Length + chunkBodySize - 1) / chunkBodySize;
        if (totalChunks > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"Fragmented payload requires {totalChunks} chunks, which exceeds the {ushort.MaxValue}-chunk wire header limit.");
        }

        byte[] headerSpan = new byte[FragmentHeader.WireSize];

        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * chunkBodySize;
            int chunkLen = Math.Min(chunkBodySize, payload.Length - offset);
            bool isLast = i == totalChunks - 1;

            FragmentHeader fragHeader = new(streamId, (ushort)i, (ushort)totalChunks, isLast);
            fragHeader.WriteTo(headerSpan);

            int framePayloadLen = FragmentHeader.WireSize + chunkLen;
            int totalFrameLen = HeaderSize + framePayloadLen;

            if (totalFrameLen > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Fragmented frame size {totalFrameLen} exceeds the {ushort.MaxValue}-byte wire header limit.");
            }

            byte[] rented = BufferLease.ByteArrayPool.Rent(totalFrameLen);

            try
            {
                BUILD_FRAGMENT_FRAME(rented.AsSpan(0, totalFrameLen), headerSpan, payload.Slice(offset, chunkLen).Span);

                await SEND_RAW_FRAME_ASYNC(rented.AsMemory(0, totalFrameLen), token).ConfigureAwait(false);
            }
            finally
            {
                BufferLease.ByteArrayPool.Return(rented);
            }
        }

        this.InvokePostCallback();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void BUILD_FRAGMENT_FRAME(Span<byte> frame, ReadOnlySpan<byte> fragHeader, ReadOnlySpan<byte> chunkBody)
        {
            // Write outer frame length (2 bytes LE)
            BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);

            // Copy FragmentHeader (có Magic)
            fragHeader.CopyTo(frame[HeaderSize..]);

            // Copy chunk body
            chunkBody.CopyTo(frame[(HeaderSize + FragmentHeader.WireSize)..]);
        }

        async Task SEND_RAW_FRAME_ASYNC(ReadOnlyMemory<byte> frame, CancellationToken token)
        {
            int sent = 0;
            while (sent < frame.Length)
            {
                int n = await _socket.SendAsync(frame[sent..], SocketFlags.None, token)
                                     .ConfigureAwait(false);

                if (n == 0)
                {
                    this.CANCEL_RECEIVE_ONCE();
                    this.INVOKE_CLOSE_ONCE();
                    throw new InvalidOperationException("The socket closed while sending.");
                }
                sent += n;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvokePostCallback()
    {
        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(_cachedArgs.Connection);
        _ = AsyncCallback.Invoke(_callbackPost, _sender, args);
    }

    #endregion
}
