// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;

#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA2254 // Template should be a static expression

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

        if (data.Length >= s_fragmentOptions.MaxChunkSize)
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

        /*
         * [Fast Path: Stack Allocation]
         * For small packets (determined by StackAllocLimit), we format the frame 
         * directly on the stack. This is zero-allocation and extremely fast.
         * The frame consists of: [2 bytes Length] + [Payload].
         */
        if (data.Length <= PacketConstants.StackAllocLimit)
        {
            try
            {
#if DEBUG
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                        $"stackalloc len={data.Length} ep={_socket.RemoteEndPoint}");
                }
#endif
                Span<byte> frameS = stackalloc byte[totalLength];
                WRITE_FRAME_HEADER(frameS, (ushort)totalLength, data);

#if DEBUG
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    Span<byte> payloadSpan = frameS.Slice(HeaderSize, data.Length);
                    _logger.LogDebug(
                        $"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
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
                        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug(
                                $"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                $"stackalloc peer-closed ep={_socket.RemoteEndPoint}");
                        }
#endif
                        this.CANCEL_RECEIVE_ONCE();
                        this.INVOKE_CLOSE_ONCE();
                        throw NetworkErrors.SendFailed;
                    }
                    sent += n;
                }

                ConnectionEventArgs? args = (_sender as Connection)?.AcquireEventArgs() ?? s_pool.Get<ConnectionEventArgs>();
                args.Initialize(_cachedArgs.Connection);

                if (!AsyncCallback.Invoke(_callbackPost, _sender, args))
                {
                    args.Dispose();
                }
                return;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (IS_BENIGN_DISCONNECT(ex))
                {
#if DEBUG
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                      $"stackalloc-benign-disconnect ep={FORMAT_ENDPOINT(_socket)} ex={ex.GetType().Name}");
                    }
#endif
                }
                else
                {
                    _sender.ThrottledError(
                        _logger,
                        "socket.send.stackalloc_error",
                        $"stackalloc-error ep={FORMAT_ENDPOINT(_socket)}", ex);
                }
                throw;
            }
        }

        /*
         * [Slow Path: Pooled Heap Buffer]
         * For larger packets that don't fit on the stack, we rent a buffer from 
         * the ArrayPool. This prevents GC pressure while still supporting large 
         * payloads.
         */
        byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);
        try
        {
#if DEBUG
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    $"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                    $"pooled len={data.Length} ep={_socket.RemoteEndPoint}");
            }
#endif
            BinaryPrimitives.WriteUInt16LittleEndian(MemoryExtensions.AsSpan(heapBuf), (ushort)totalLength);
            data.CopyTo(MemoryExtensions.AsSpan(heapBuf, HeaderSize));

#if DEBUG
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                Span<byte> payloadSpan = MemoryExtensions.AsSpan(heapBuf, HeaderSize, data.Length);
                _logger.LogDebug(
                    $"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
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
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "[NW.{Class}:{Method}] pooled peer-closed ep={Ep}",
                            nameof(SocketConnection), nameof(Send), _socket.RemoteEndPoint);
                    }
#endif
                    this.CANCEL_RECEIVE_ONCE();
                    this.INVOKE_CLOSE_ONCE();
                    throw NetworkErrors.SendFailed;
                }
                sent += n;
            }

            this.InvokePostCallback();
            return;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (IS_BENIGN_DISCONNECT(ex))
            {
#if DEBUG
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "[NW.{Class}:{Method}] pooled-benign-disconnect ep={Ep} ex={ExType}",
                        nameof(SocketConnection), nameof(Send), FORMAT_ENDPOINT(_socket), ex.GetType().Name);
                }
#endif
            }
            else
            {
                _sender.ThrottledError(
                    _logger,
                    "socket.send.pooled_error",
                    $"pooled-error ep={FORMAT_ENDPOINT(_socket)}", ex);
            }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        this.THROW_IF_NOT_CONFIGURED();

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, nameof(SocketConnection));

        if (data.IsEmpty)
        {
            return ValueTask.FromException(new ArgumentException("Data must not be empty.", nameof(data)));
        }

        if (data.Length >= s_fragmentOptions.MaxChunkSize)
        {
            return this.SEND_FRAGMENTED_ASYNC(data, cancellationToken);
        }

        int totalLength = data.Length + HeaderSize;
        if (totalLength > ushort.MaxValue)
        {
            return ValueTask.FromException(new ArgumentOutOfRangeException(
                nameof(data),
                totalLength,
                $"Non-fragmented frame size must not exceed {ushort.MaxValue} bytes."));
        }

        byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);

        try
        {
#if DEBUG
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "[NW.{Class}:{Method}] len={Len} ep={Ep}",
                    nameof(SocketConnection), nameof(SendAsync), data.Length, _socket.RemoteEndPoint);
            }
#endif
            WRITE_FRAME_HEADER(MemoryExtensions.AsSpan(heapBuf), (ushort)totalLength, data.Span);

#if DEBUG
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                ReadOnlySpan<byte> payloadSpan = data.Span;

                _logger.LogDebug(
                    "[NW.{Class}:{Method}] sending frame totalLen={Total} payload={Payload} ep={Ep}",
                    nameof(SocketConnection), nameof(SendAsync), totalLength, FORMAT_FRAME_FOR_LOG(payloadSpan), _socket.RemoteEndPoint);
            }
#endif

            int sent = 0;
            while (sent < totalLength)
            {
                ValueTask<int> vt = _socket.SendAsync(MemoryExtensions
                                           .AsMemory(heapBuf, sent, totalLength - sent), SocketFlags.None, cancellationToken);

                if (vt.IsCompletedSuccessfully)
                {
                    int n = vt.Result;
                    if (n == 0)
                    {
                        return HANDLE_PEER_CLOSED(this, heapBuf);
                    }
                    sent += n;
                }
                else
                {
                    return AWAIT_SEND(this, vt, heapBuf, sent, totalLength, cancellationToken);
                }
            }

            this.InvokePostCallback();
            BufferLease.ByteArrayPool.Return(heapBuf);
            return default;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
            return HANDLE_SEND_ERROR(this, ex);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AWAIT_SEND(SocketConnection self, ValueTask<int> vt, byte[] heapBuf, int sent, int totalLength, CancellationToken token)
        {
            try
            {
                int n = await vt.ConfigureAwait(false);
                if (n == 0)
                {
                    throw HANDLE_PEER_CLOSED_EXCEPTION(self);
                }
                sent += n;

                while (sent < totalLength)
                {
                    n = await self._socket.SendAsync(MemoryExtensions.AsMemory(heapBuf, sent, totalLength - sent), SocketFlags.None, token).ConfigureAwait(false);
                    if (n == 0)
                    {
                        throw HANDLE_PEER_CLOSED_EXCEPTION(self);
                    }
                    sent += n;
                }

                self.InvokePostCallback();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                throw HANDLE_SEND_ERROR_EXCEPTION(self, ex);
            }
            finally
            {
                BufferLease.ByteArrayPool.Return(heapBuf);
            }
        }

        static ValueTask HANDLE_PEER_CLOSED(SocketConnection self, byte[] buf)
        {
            BufferLease.ByteArrayPool.Return(buf);
            self.CANCEL_RECEIVE_ONCE();
            self.INVOKE_CLOSE_ONCE();
            return ValueTask.FromException(NetworkErrors.SendFailed);
        }

        static Exception HANDLE_PEER_CLOSED_EXCEPTION(SocketConnection self)
        {
            self.CANCEL_RECEIVE_ONCE();
            self.INVOKE_CLOSE_ONCE();
            return NetworkErrors.SendFailed;
        }

        static ValueTask HANDLE_SEND_ERROR(SocketConnection self, Exception ex)
        {
            if (!IS_BENIGN_DISCONNECT(ex))
            {
                self._sender.ThrottledError(self._logger, "socket.send.error", $"error ep={FORMAT_ENDPOINT(self._socket)}", ex);
            }
            return ValueTask.FromException(ex);
        }

        static Exception HANDLE_SEND_ERROR_EXCEPTION(SocketConnection self, Exception ex)
        {
            if (!IS_BENIGN_DISCONNECT(ex))
            {
                self._sender.ThrottledError(self._logger, "socket.send.error", $"error ep={FORMAT_ENDPOINT(self._socket)}", ex);
            }
            return ex;
        }
    }

    #region Fragmented Send Helpers

    /// <summary>
    /// Sends large payloads by fragmenting them automatically.
    /// The caller does not need to split the payload manually; this method
    /// turns it into the wire format expected by the receiver and sends each
    /// fragment in order.
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
        int chunkBodySize = s_fragmentOptions.MaxChunkSize;
        int totalChunks = (payload.Length + chunkBodySize - 1) / chunkBodySize;
        if (totalChunks > ushort.MaxValue)
        {
            throw new InternalErrorException(
                $"Fragmented payload requires {totalChunks} chunks, which exceeds the {ushort.MaxValue}-chunk wire header limit.");
        }

        Span<byte> headerBuffer = stackalloc byte[FragmentHeader.WireSize];

        /*
         * [Fragmentation Logic]
         * When a payload exceeds MaxChunkSize, we split it into multiple 
         * fragments. Each fragment is wrapped in a FragmentHeader and sent 
         * as a separate wire frame. The receiver will reassemble these chunks.
         */
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

            // Compute the full wire size for this fragment, including the outer
            // length prefix and the fragment header.
            int framePayloadSize = FragmentHeader.WireSize + thisChunkSize;

            int totalFrameSize = HeaderSize + framePayloadSize;

            if (totalFrameSize > ushort.MaxValue)
            {
                throw new InternalErrorException(
                    $"Fragmented frame size {totalFrameSize} exceeds the {ushort.MaxValue}-byte wire header limit.");
            }

            /*
             * Small fragments stay on the stack to avoid renting a buffer.
             * This keeps the fast path allocation-free for fragments that fit
             * comfortably within the stack allocation limit.
             */
            if (totalFrameSize <= PacketConstants.StackAllocLimit)
            {
                Span<byte> frame = stackalloc byte[totalFrameSize];

                // Write the outer frame length.
                BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)totalFrameSize);

                // Write FragmentHeader + magic + chunk body.
                headerBuffer.CopyTo(frame[HeaderSize..]);
                payload.Slice(offset, thisChunkSize).CopyTo(frame[(HeaderSize + FragmentHeader.WireSize)..]);

                SEND_RAW_FRAME(frame);
            }
            else
            {
                /*
                 * Large fragments use a pooled buffer so we do not allocate on
                 * every chunk and can reuse the same memory pressure budget.
                 */
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
                    throw NetworkErrors.SendFailed;
                }
                sent += n;
            }
        }
    }

    private ValueTask SEND_FRAGMENTED_ASYNC(ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        if (payload.Length > s_fragmentOptions.MaxPayloadSize)
        {
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(payload),
                $"Payload exceeds maximum allowed size {s_fragmentOptions.MaxPayloadSize}"));
        }

        ushort streamId = FragmentStreamId.Next();
        int chunkBodySize = s_fragmentOptions.MaxChunkSize;
        int totalChunks = (payload.Length + chunkBodySize - 1) / chunkBodySize;
        if (totalChunks > ushort.MaxValue)
        {
            return ValueTask.FromException(new InternalErrorException(
                $"Fragmented payload requires {totalChunks} chunks, which exceeds the {ushort.MaxValue}-chunk wire header limit."));
        }

        return SEND_CHUNKS_ASYNC(this, payload, streamId, chunkBodySize, totalChunks, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask SEND_CHUNKS_ASYNC(SocketConnection self, ReadOnlyMemory<byte> payload, ushort streamId, int chunkBodySize, int totalChunks, CancellationToken token)
        {
            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * chunkBodySize;
                int chunkLen = Math.Min(chunkBodySize, payload.Length - offset);
                bool isLast = i == totalChunks - 1;

                int framePayloadLen = FragmentHeader.WireSize + chunkLen;
                int totalFrameLen = HeaderSize + framePayloadLen;

                if (totalFrameLen > ushort.MaxValue)
                {
                    throw new InternalErrorException(
                        $"Fragmented frame size {totalFrameLen} exceeds the {ushort.MaxValue}-byte wire header limit.");
                }

                byte[] rented = BufferLease.ByteArrayPool.Rent(totalFrameLen);

                try
                {
                    // Write header directly into the rented buffer to avoid Span across await.
                    FragmentHeader fragHeader = new(streamId, (ushort)i, (ushort)totalChunks, isLast);
                    fragHeader.WriteTo(rented.AsSpan(HeaderSize, FragmentHeader.WireSize));

                    BUILD_FRAGMENT_FRAME(rented.AsSpan(0, totalFrameLen), payload.Slice(offset, chunkLen).Span);

                    await SEND_RAW_FRAME_ASYNC(self, rented.AsMemory(0, totalFrameLen), token).ConfigureAwait(false);
                }
                finally
                {
                    BufferLease.ByteArrayPool.Return(rented);
                }
            }

            self.InvokePostCallback();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void BUILD_FRAGMENT_FRAME(Span<byte> frame, ReadOnlySpan<byte> chunkBody)
        {
            // Write the outer frame length first because the receiver reads this
            // prefix before it can interpret the fragment header or payload body.
            BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);

            // Copy the chunk body into the wire frame immediately after the
            // fragment header (which was already written).
            chunkBody.CopyTo(frame[(HeaderSize + FragmentHeader.WireSize)..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ValueTask SEND_RAW_FRAME_ASYNC(SocketConnection self, ReadOnlyMemory<byte> frame, CancellationToken token)
        {
            int sent = 0;
            while (sent < frame.Length)
            {
                ValueTask<int> vt = self._socket.SendAsync(frame[sent..], SocketFlags.None, token);
                if (vt.IsCompletedSuccessfully)
                {
                    int n = vt.Result;
                    if (n == 0)
                    {
                        self.CANCEL_RECEIVE_ONCE();
                        self.INVOKE_CLOSE_ONCE();
                        return ValueTask.FromException(NetworkErrors.SendFailed);
                    }
                    sent += n;
                }
                else
                {
                    return AWAIT_RAW_SEND(self, vt, frame, sent, token);
                }
            }

            return default;

            [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
            static async ValueTask AWAIT_RAW_SEND(SocketConnection self, ValueTask<int> vt, ReadOnlyMemory<byte> frame, int sent, CancellationToken token)
            {
                int n = await vt.ConfigureAwait(false);
                if (n == 0)
                {
                    self.CANCEL_RECEIVE_ONCE();
                    self.INVOKE_CLOSE_ONCE();
                    throw NetworkErrors.SendFailed;
                }
                sent += n;

                while (sent < frame.Length)
                {
                    n = await self._socket.SendAsync(frame[sent..], SocketFlags.None, token).ConfigureAwait(false);
                    if (n == 0)
                    {
                        self.CANCEL_RECEIVE_ONCE();
                        self.INVOKE_CLOSE_ONCE();
                        throw NetworkErrors.SendFailed;
                    }
                    sent += n;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvokePostCallback()
    {
        ConnectionEventArgs? args = (_sender as Connection)?.AcquireEventArgs() ?? s_pool.Get<ConnectionEventArgs>();
        args.Initialize(_cachedArgs.Connection);
        if (!AsyncCallback.Invoke(_callbackPost, _sender, args))
        {
            args.Dispose();
        }
    }

    #endregion
}
