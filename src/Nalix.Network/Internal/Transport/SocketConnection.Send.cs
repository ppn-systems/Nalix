// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
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
    /// <returns><see langword="true"/> if the data was sent successfully.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Send(ReadOnlySpan<byte> data)
    {
        THROW_IF_NOT_CONFIGURED();

        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > PacketConstants.PacketSizeLimit - HeaderSize)
        {
            throw new ArgumentOutOfRangeException(nameof(data),
                $"Packet size {data.Length} exceeds limit {PacketConstants.PacketSizeLimit - HeaderSize}");
        }

        ushort totalLength = (ushort)(data.Length + HeaderSize);

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
                WRITE_FRAME_HEADER(frameS, totalLength, data);

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
                        CANCEL_RECEIVE_ONCE();
                        INVOKE_CLOSE_ONCE();
                        return false;
                    }
                    sent += n;
                }

                ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
                args.Initialize(_cachedArgs.Connection);

                AsyncCallback.Invoke(_callbackPost, _sender, args);
                return true;
            }
            catch (Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
                                $"stackalloc-error ep={_socket.RemoteEndPoint}", ex);
                return false;
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
            System.Buffers.Binary.BinaryPrimitives
                .WriteUInt16LittleEndian(MemoryExtensions.AsSpan(heapBuf), totalLength);
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
                    CANCEL_RECEIVE_ONCE();
                    INVOKE_CLOSE_ONCE();
                    return false;
                }
                sent += n;
            }

            ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
            args.Initialize(_cachedArgs.Connection);

            AsyncCallback.Invoke(_callbackPost, _sender, args);
            return true;
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(SocketConnection)}:{nameof(Send)}] " +
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
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns><see langword="true"/> if the data was sent successfully.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task<bool> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        THROW_IF_NOT_CONFIGURED();

        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > PacketConstants.PacketSizeLimit - HeaderSize)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        ushort totalLength = (ushort)(data.Length + HeaderSize);
        byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);

        try
        {
#if DEBUG
            s_logger?.Debug($"[NW.{nameof(SocketConnection)}:{nameof(SendAsync)}] " +
                            $"len={data.Length} ep={_socket.RemoteEndPoint}");
#endif
            WRITE_FRAME_HEADER(MemoryExtensions.AsSpan(heapBuf), totalLength, data.Span);

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
                    CANCEL_RECEIVE_ONCE();
                    INVOKE_CLOSE_ONCE();
                    return false;
                }
                sent += n;
            }

            ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
            args.Initialize(_cachedArgs.Connection);

            AsyncCallback.Invoke(_callbackPost, _sender, args);
            return true;
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(SocketConnection)}:{nameof(SendAsync)}] " +
                            $"error ep={_socket.RemoteEndPoint}", ex);
            return false;
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
        }
    }
}
