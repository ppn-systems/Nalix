// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Codec.Transforms;
using Nalix.Environment.Memory;
using Nalix.Environment.Sequencing;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal.Web;

internal sealed class WsFrameReader : IDisposable
{
    private readonly SequenceCounter _sequence;
    private readonly TransportOptions _options;
    private readonly Func<ClientWebSocket> _getSocket;
    private readonly Action<IBufferLease> _onMessage;
    private readonly Action<Exception> _onError;

    private int _disposed;

    public WsFrameReader(
        Func<ClientWebSocket> getSocket,
        TransportOptions options,
        Action<IBufferLease> onMessage,
        Action<Exception> onError)
    {
        _sequence = new SequenceCounter();
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
        _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
    }

    public async Task ReceiveLoopAsync(CancellationToken ct)
    {
        ClientWebSocket socket = _getSocket();
        byte[] buffer = BufferLease.ByteArrayPool.Rent(8192);

        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.EndOfMessage)
                {
                    // Fast path: the whole message fits in the rented buffer
                    this.PROCESS_FRAME(buffer.AsSpan(0, result.Count));
                }
                else
                {
                    // Slow path: large message spanning multiple frames
                    await this.RECEIVE_LARGE_FRAME_ASYNC(socket, buffer, result.Count, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            _onError?.Invoke(ex);
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);
        }
    }

    private async Task RECEIVE_LARGE_FRAME_ASYNC(ClientWebSocket socket, byte[] initialBuffer, int initialCount, CancellationToken ct)
    {
        using MemoryStream ms = new();
        await ms.WriteAsync(initialBuffer.AsMemory(0, initialCount), ct).ConfigureAwait(false);

        byte[] tempBuffer = BufferLease.ByteArrayPool.Rent(65536);
        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(tempBuffer), ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
                await ms.WriteAsync(tempBuffer.AsMemory(0, result.Count), ct).ConfigureAwait(false);
            }
            while (!result.EndOfMessage);

            if (ms.TryGetBuffer(out ArraySegment<byte> fullBuffer))
            {
                this.PROCESS_FRAME(fullBuffer.AsSpan());
            }
            else
            {
                this.PROCESS_FRAME(ms.ToArray());
            }
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(tempBuffer);
        }
    }

    private void PROCESS_FRAME(ReadOnlySpan<byte> frameData)
    {
        IBufferLease lease = BufferLease.Rent(frameData.Length);
        frameData.CopyTo(lease.SpanFull);
        lease.CommitLength(frameData.Length);

        IBufferLease original = lease;
        try
        {
            FramePipeline.ProcessInbound(
                ref lease,
                _options.Secret.AsSpan(),
                _options.Algorithm,
                out uint? seq);

            if (!_sequence.IsValid(seq))
            {
                return;
            }

            _onMessage(lease);

            if (seq.HasValue)
            {
                _sequence.UpdateTo(seq.Value);
            }
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            _onError?.Invoke(ex);
        }
        finally
        {
            if (!ReferenceEquals(lease, original))
            {
                lease.Dispose();
            }

            original.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }
    }
}
