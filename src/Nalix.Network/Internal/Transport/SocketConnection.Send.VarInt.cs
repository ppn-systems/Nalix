// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Diagnostics;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Environment.Memory;

namespace Nalix.Network.Internal.Transport;

internal sealed partial class SocketConnection
{
    private void SEND_VARINT(ReadOnlySpan<byte> data)
    {
        int varIntSize = Leb128.GetByteCount(data.Length);
        long totalLengthLong = (long)data.Length + varIntSize;

        if (totalLengthLong > _maxVarIntPayloadSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                totalLengthLong,
                $"VarInt payload exceeds the maximum allowed size of {_maxVarIntPayloadSize}.");
        }

        int totalLength = (int)totalLengthLong;

        if (totalLength <= PacketConstants.StackAllocLimit)
        {
            try
            {
#if DEBUG
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.SocketConnection:Internal", $"stackalloc varint length={data.Length} remote-endpoint={_socket.RemoteEndPoint}"));
                }
#endif
                Span<byte> frameS = stackalloc byte[totalLength];
                WRITE_VARINT_FRAME_HEADER(frameS, data.Length, varIntSize, data);

                lock (_sendLock)
                {
                    int sent = 0;
                    while (sent < frameS.Length)
                    {
                        int n = _socket.Send(frameS[sent..]);
                        if (n == 0)
                        {
                            this.CANCEL_RECEIVE_ONCE();
                            this.INVOKE_CLOSE_ONCE();
                            Throw.SendFailedNow();
                        }
                        sent += n;
                        Interlocked.Add(ref _bytesSent, n);
                    }
                }
                _sink.OnFrameSent(_owner);
                return;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (IS_BENIGN_DISCONNECT(ex)) { /* benign */ }
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                    {
                        if (Internal.Security.ThrottledEventGate.TryAcquire(ref s_sendVarIntErrorTicks, ref s_sendVarIntErrorSuppressed, DateTime.UtcNow.Ticks, TimeSpan.TicksPerSecond * 5, out long suppressed))
                        {
                            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.SocketConnection:Internal", $"varint send error endpoint={_endpointString} suppressed-count={suppressed}", ex));
        };
                        }
                    }
                }
                throw;
            }
        }

        byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);
        try
        {
            WRITE_VARINT_FRAME_HEADER(MemoryExtensions.AsSpan(heapBuf), data.Length, varIntSize, data);

            lock (_sendLock)
            {
                int sent = 0;
                while (sent < totalLength)
                {
                    int n = _socket.Send(heapBuf, sent, totalLength - sent, SocketFlags.None);
                    if (n == 0)
                    {
                        this.CANCEL_RECEIVE_ONCE();
                        this.INVOKE_CLOSE_ONCE();
                        Throw.SendFailedNow();
                    }
                    sent += n;
                    Interlocked.Add(ref _bytesSent, n);
                }
            }

            this.INVOKE_POST_CALLBACK();
            return;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (IS_BENIGN_DISCONNECT(ex)) { /* benign */ }
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                {
                    if (Internal.Security.ThrottledEventGate.TryAcquire(ref s_sendVarIntErrorTicks, ref s_sendVarIntErrorSuppressed, DateTime.UtcNow.Ticks, TimeSpan.TicksPerSecond * 5, out long suppressed))
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.SocketConnection:Internal", $"varint send error endpoint={_endpointString} suppressed-count={suppressed}", ex));
        };
                    }
                }
            }
            throw;
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
        }
    }

    private ValueTask SEND_VARINT_ASYNC(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        int varIntSize = Leb128.GetByteCount(data.Length);
        long totalLengthLong = (long)data.Length + varIntSize;

        if (totalLengthLong > _maxVarIntPayloadSize)
        {
            return ValueTask.FromException(new ArgumentOutOfRangeException(
                nameof(data),
                totalLengthLong,
                $"VarInt payload exceeds the maximum allowed size of {_maxVarIntPayloadSize}."));
        }

        int totalLength = (int)totalLengthLong;
        byte[] heapBuf = BufferLease.ByteArrayPool.Rent(totalLength);

        try
        {
            WRITE_VARINT_FRAME_HEADER(MemoryExtensions.AsSpan(heapBuf), data.Length, varIntSize, data.Span);

            int sent = 0;
            while (sent < totalLength)
            {
                ValueTask<int> vt = _socket.SendAsync(MemoryExtensions.AsMemory(heapBuf, sent, totalLength - sent), SocketFlags.None, cancellationToken);
                if (vt.IsCompletedSuccessfully)
                {
                    int n = vt.Result;
                    if (n == 0)
                    {
                        BufferLease.ByteArrayPool.Return(heapBuf);
                        this.CANCEL_RECEIVE_ONCE();
                        this.INVOKE_CLOSE_ONCE();
                        return ValueTask.FromException(Throw.GetSendFailed());
                    }
                    sent += n;
                    _ = Interlocked.Add(ref _bytesSent, n);
                }
                else
                {
                    return AWAIT_VARINT_SEND(this, vt, heapBuf, sent, totalLength, cancellationToken);
                }
            }

            this.INVOKE_POST_CALLBACK();
            BufferLease.ByteArrayPool.Return(heapBuf);
            return default;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            BufferLease.ByteArrayPool.Return(heapBuf);
            if (!IS_BENIGN_DISCONNECT(ex))
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                {
                    if (Internal.Security.ThrottledEventGate.TryAcquire(ref s_sendVarIntErrorTicks, ref s_sendVarIntErrorSuppressed, DateTime.UtcNow.Ticks, TimeSpan.TicksPerSecond * 5, out long suppressed))
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.SocketConnection:Internal", $"varint send error endpoint={_endpointString} suppressed-count={suppressed}", ex));
        };
                    }
                }
            }
            return ValueTask.FromException(ex);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AWAIT_VARINT_SEND(SocketConnection self, ValueTask<int> vt, byte[] heapBuf, int sent, int totalLength, CancellationToken token)
        {
            try
            {
                int n = await vt.ConfigureAwait(false);
                if (n == 0)
                {
                    throw HANDLE_PEER_CLOSED_EXCEPTION(self);
                }

                sent += n;
                _ = Interlocked.Add(ref self._bytesSent, n);

                while (sent < totalLength)
                {
                    n = await self._socket.SendAsync(MemoryExtensions.AsMemory(heapBuf, sent, totalLength - sent), SocketFlags.None, token).ConfigureAwait(false);
                    if (n == 0)
                    {
                        throw HANDLE_PEER_CLOSED_EXCEPTION(self);
                    }

                    sent += n;
                    _ = Interlocked.Add(ref self._bytesSent, n);
                }
                self.INVOKE_POST_CALLBACK();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (!IS_BENIGN_DISCONNECT(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                    {
                        if (Internal.Security.ThrottledEventGate.TryAcquire(ref s_sendVarIntErrorTicks, ref s_sendVarIntErrorSuppressed, DateTime.UtcNow.Ticks, TimeSpan.TicksPerSecond * 5, out long suppressed))
                        {
                            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.SocketConnection:Internal", $"varint send error endpoint={self._endpointString} suppressed-count={suppressed}", ex));
        };
                        }
                    }
                }
                throw;
            }
            finally
            {
                BufferLease.ByteArrayPool.Return(heapBuf);
            }
        }

        static Exception HANDLE_PEER_CLOSED_EXCEPTION(SocketConnection self)
        {
            self.CANCEL_RECEIVE_ONCE();
            self.INVOKE_CLOSE_ONCE();
            return Throw.GetSendFailed();
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WRITE_VARINT_FRAME_HEADER(Span<byte> buffer, int payloadLength, int varIntSize, ReadOnlySpan<byte> payload)
    {
        _ = Leb128.Write(buffer, payloadLength);
        payload.CopyTo(buffer[varIntSize..]);
    }
}
