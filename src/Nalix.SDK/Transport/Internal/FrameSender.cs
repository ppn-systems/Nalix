// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Optimized frame sender that serializes all outbound frames through a bounded channel
/// to prevent message interleaving. Handles chunking/fragmentation of large payloads.
/// </summary>
internal sealed class FrameSender : IDisposable
{
    #region Fields

    private readonly TransportOptions _options;
    private readonly FragmentOptions _fragmentOptions;
    private readonly Func<Socket> _getSocket;
    private readonly Action<Exception> _onError;

    private readonly Channel<(byte[] frame, int frameLen, TaskCompletionSource<bool> tcs)> _sendQueue;
    private readonly CancellationTokenSource _drainCts = new();
    private int _disposed;

    #endregion Fields

    public FrameSender(Func<Socket> getSocket, TransportOptions options, Action<Exception> onError)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();
        _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));

        _sendQueue = Channel.CreateBounded<(byte[], int, TaskCompletionSource<bool>)>(
            new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });

        _ = Task.Run(() => this.DRAIN_LOOP_ASYNC(_drainCts.Token));
    }

    public async Task<bool> SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
    {
        // ── Transformation ──────────────────────────────────────────────────────
        BufferLease current = BufferLease.CopyFrom(payload.Span);
        try
        {
            PacketFrameTransforms.TransformOutbound(ref current, _options, encrypt ?? _options.EncryptionEnabled);

            // ── After transformation, check for fragmentation ────────────────────
            if (current.Length >= _fragmentOptions.MaxChunkSize)
            {
                return await this.SEND_FRAGMENTED_ASYNC(current.Memory, ct).ConfigureAwait(false);
            }

            // ── Standard Framing ──────────────────────────────────────────────────
            int totalLen = TcpSession.HeaderSize + current.Length;
            byte[] frame = BufferLease.ByteArrayPool.Rent(totalLen);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, TcpSession.HeaderSize), (ushort)totalLen);
            current.Memory.Span.CopyTo(frame.AsSpan(TcpSession.HeaderSize, current.Length));

            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await _sendQueue.Writer.WriteAsync((frame, totalLen, tcs), ct).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally { current.Dispose(); }
    }

    #region Private Methods

    private async Task DRAIN_LOOP_ASYNC(CancellationToken token)
    {
        ChannelReader<(byte[] frame, int frameLen, TaskCompletionSource<bool> tcs)> reader = _sendQueue.Reader;
        try
        {
            while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (reader.TryRead(out (byte[] frame, int frameLen, TaskCompletionSource<bool> tcs) item))
                {
                    await this.SEND_SOCKET_ASYNC(item.frame, item.frameLen, item.tcs, token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _onError?.Invoke(ex); }
    }

    private async Task SEND_SOCKET_ASYNC(byte[] frame, int frameLen, TaskCompletionSource<bool> tcs, CancellationToken token)
    {
        try
        {
            Socket s = _getSocket();
            int sent = 0;
            while (sent < frameLen)
            {
                int n = await s.SendAsync(new ReadOnlyMemory<byte>(frame, sent, frameLen - sent), SocketFlags.None, token).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                sent += n;
            }
            _ = tcs.TrySetResult(true);
        }
        catch (Exception)
        {
            _ = tcs.TrySetResult(false);
            throw;
        }
        finally { BufferLease.ByteArrayPool.Return(frame); }
    }

    private async Task<bool> SEND_FRAGMENTED_ASYNC(ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        if (payload.Length > _fragmentOptions.MaxPayloadSize)
        {
            throw new ArgumentOutOfRangeException(nameof(payload),
                $"Payload exceeds MaxPayloadSize {_fragmentOptions.MaxPayloadSize}");
        }

        ushort streamId = FragmentStreamId.Next();
        int chunkBodySize = _fragmentOptions.MaxChunkSize;
        int totalChunks = (payload.Length + chunkBodySize - 1) / chunkBodySize;
        if (totalChunks > ushort.MaxValue)
        {
            throw new InternalErrorException(
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

            int totalFrameLen = TcpSession.HeaderSize + FragmentHeader.WireSize + chunkLen;

            if (totalFrameLen > ushort.MaxValue)
            {
                throw new InternalErrorException(
                    $"Fragmented frame size {totalFrameLen} exceeds the {ushort.MaxValue}-byte wire header limit.");
            }

            byte[] frame = BufferLease.ByteArrayPool.Rent(totalFrameLen);

            try
            {
                BUILD_FRAGMENT_FRAME(frame.AsSpan(0, totalFrameLen), headerSpan, payload.Slice(offset, chunkLen).Span);

                bool sent = await ENQUEUE_FRAME_ASYNC(frame, totalFrameLen, token).ConfigureAwait(false);
                if (!sent)
                {
                    return false;
                }
            }
            catch
            {
                BufferLease.ByteArrayPool.Return(frame);
                throw;
            }
        }

        return true;


        async Task<bool> ENQUEUE_FRAME_ASYNC(byte[] frame, int frameLen, CancellationToken token)
        {
            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                await _sendQueue.Writer.WriteAsync((frame, frameLen, tcs), token).ConfigureAwait(false);
            }
            catch
            {
                BufferLease.ByteArrayPool.Return(frame);
                throw;
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void BUILD_FRAGMENT_FRAME(Span<byte> frame, ReadOnlySpan<byte> fragHeader, ReadOnlySpan<byte> chunkBody)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
            fragHeader.CopyTo(frame[TcpSession.HeaderSize..]);
            chunkBody.CopyTo(frame[(TcpSession.HeaderSize + FragmentHeader.WireSize)..]);
        }
    }

    #endregion Private Methods

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _drainCts.Cancel();
        _drainCts.Dispose();
        _ = _sendQueue.Writer.TryComplete();
    }
}
