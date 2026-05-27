// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Environment.Memory;
using Nalix.Environment.Time;

namespace Nalix.Network.Internal.Transport;

internal sealed partial class SocketConnection
{
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task SAEA_RECEIVE_LOOP_VARINT_ASYNC(CancellationToken token)
    {
        try
        {
            while (Volatile.Read(ref _disposed) == 0 && !token.IsCancellationRequested)
            {
                int consumed = 0;
                bool incomplete = false;
                int? pendingFrameSize = null;
                bool parsedAtLeastOne = false;

                while (_bufferDataLength - consumed > 0)
                {
                    // 1. Attempt to read the VarInt header.
                    ReadOnlySpan<byte> currentBuffer = MemoryExtensions.AsSpan(_buffer!, consumed, _bufferDataLength - consumed);
                    if (!Leb128.TryRead(currentBuffer, out int payloadLen, out int varIntBytesRead))
                    {
                        // Incomplete VarInt header. Wait for more data.
                        incomplete = true;
                        break;
                    }

                    if (payloadLen < 0 || payloadLen > _maxVarIntPayloadSize)
                    {
#if DEBUG
                        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"[NW.{nameof(SocketConnection)}] varint invalid-size={payloadLen} max={_maxVarIntPayloadSize} ep={_endpointString}");
                        }
#endif
                        throw new InternalErrorException($"VarInt payload size {payloadLen} is invalid or exceeds the maximum allowed size of {_maxVarIntPayloadSize}.");
                    }

                    int totalFrameSize = varIntBytesRead + payloadLen;

                    // 2. Check if the full frame is in the buffer.
                    if (_bufferDataLength - consumed < totalFrameSize)
                    {
                        pendingFrameSize = totalFrameSize;
                        break;
                    }

                    // 3. We have the full frame. Dispatch it.
                    this.PROCESS_VARINT_FRAME_FROM_BUFFER(consumed + varIntBytesRead, payloadLen);

                    consumed += totalFrameSize;
                    parsedAtLeastOne = true;
                }

                // Buffer Compaction
                if (consumed > 0)
                {
                    int remaining = _bufferDataLength - consumed;
                    if (remaining > 0)
                    {
                        Buffer.BlockCopy(_buffer!, consumed, _buffer!, 0, remaining);
                    }

                    _bufferDataLength = remaining;
                }

                // Elastic Resizing
                if (pendingFrameSize.HasValue)
                {
                    int requiredSize = pendingFrameSize.Value;

                    if (requiredSize > _buffer!.Length)
                    {
                        byte[] newBuffer = BufferLease.ByteArrayPool.Rent(requiredSize);

                        if (_bufferDataLength > 0)
                        {
                            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _bufferDataLength);
                        }

                        BufferLease.ByteArrayPool.Return(_buffer);
                        _buffer = newBuffer;
                    }
                }
                else if (_bufferDataLength == 0 && _buffer!.Length > s_fragmentOptions.MinReceiveBufferSize)
                {
                    BufferLease.ByteArrayPool.Return(_buffer);
                    _buffer = BufferLease.ByteArrayPool.Rent(s_fragmentOptions.MinReceiveBufferSize);
                }

                // Opportunistic Read
                if (!parsedAtLeastOne || _bufferDataLength == 0 || incomplete)
                {
                    await this.RECEIVE_OPPORTUNISTIC_ASYNC(token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (IS_BENIGN_DISCONNECT(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_VARINT_ASYNC)}] " +
                    $"ended (peer closed/shutdown) ep={_owner?.NetworkEndpoint.Address}");
            }
        }
        catch (OperationCanceledException)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    $"[NW.{nameof(SocketConnection)}:{nameof(SAEA_RECEIVE_LOOP_VARINT_ASYNC)}] " +
                    $"cancelled ep={_owner?.NetworkEndpoint.Address}");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                Exception e = (ex as AggregateException)?.Flatten() ?? ex;

                _owner.ThrottledError(
                    _logger, "socket.receive.varint.faulted",
                    $"faulted ep={_owner.NetworkEndpoint.Address}", e);
            }
        }
        finally
        {
            this.CANCEL_RECEIVE_ONCE();
            this.INVOKE_CLOSE_ONCE();
        }
    }

    private void PROCESS_VARINT_FRAME_FROM_BUFFER(int offset, int payloadLen)
    {
        ReadOnlySpan<byte> rawPayloadSpan = MemoryExtensions.AsSpan(_buffer, offset, payloadLen);

        BufferLease lease = BufferLease.CopyFrom(rawPayloadSpan);
        lease.IsReliable = true;

        this.LastPingTime = Clock.UnixMillisecondsNow();

        if (!_sink.OnFrameReceived(_owner, lease, isReliable: true))
        {
#if DEBUG
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"[NW.{nameof(SocketConnection)}] varint frame-dropped " +
                                  $"length={payloadLen} ep={_endpointString}");
            }
#endif
            lease.Dispose();
        }

#if DEBUG
        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                $"[NW.{nameof(SocketConnection)}] varint handoff-to-sink " +
                $"payload={payloadLen} ep={_endpointString}");
        }
#endif
    }
}
