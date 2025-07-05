// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.SDK.Transport.Internal;

internal sealed class FRAME_READER(
    System.Func<System.Net.Sockets.Socket> getSocket,
    TransportOptions options,
    System.Action<IBufferLease> onMessage,
    System.Action<System.Exception> onError,
    System.Action<System.Int32> reportBytesReceived)
{
    private readonly BufferPoolManager _bufferPool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

    private readonly TransportOptions _options = options ?? throw new System.ArgumentNullException(nameof(options));
    private readonly System.Func<System.Net.Sockets.Socket> _getSocket = getSocket ?? throw new System.ArgumentNullException(nameof(getSocket));
    private readonly System.Action<System.Exception> _onError = onError ?? throw new System.ArgumentNullException(nameof(onError));
    private readonly System.Action<IBufferLease> _onMessage = onMessage ?? throw new System.ArgumentNullException(nameof(onMessage));
    private readonly System.Action<System.Int32> _reportBytesReceived = reportBytesReceived ?? throw new System.ArgumentNullException(nameof(reportBytesReceived));

    public async System.Threading.Tasks.Task ReceiveLoopAsync(System.Threading.CancellationToken token)
    {
        System.Net.Sockets.Socket s;
        try
        {
            s = _getSocket();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[SDK.{nameof(FRAME_READER)}] receive-start-error {ex.Message}", ex);
            _onError(ex);
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1) Read 2-byte length header
                System.Byte[] headerBuffer =
                    System.Buffers.ArrayPool<System.Byte>.Shared.Rent(ReliableClient.HeaderSize);
                try
                {
                    var headerMemory = new System.Memory<System.Byte>(
                        headerBuffer, 0, ReliableClient.HeaderSize);

                    await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);

                    System.UInt16 totalLen =
                        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(headerMemory.Span);

                    if (totalLen < ReliableClient.HeaderSize || totalLen > _options.MaxPacketSize)
                    {
                        throw new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.ProtocolNotSupported);
                    }

                    System.Int32 payloadLen = totalLen - ReliableClient.HeaderSize;

                    // 2) Rent buffer for full frame and read payload
                    System.Byte[] rented = _bufferPool.Rent(totalLen);
                    System.Boolean ownershipTransferred = false;
                    try
                    {
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                            System.MemoryExtensions.AsSpan(rented, 0, ReliableClient.HeaderSize),
                            totalLen);

                        if (payloadLen > 0)
                        {
                            await RECEIVE_EXACTLY_ASYNC(
                                s,
                                System.MemoryExtensions.AsMemory(
                                    rented, ReliableClient.HeaderSize, payloadLen),
                                token).ConfigureAwait(false);
                        }

                        // Best-effort telemetry — never throw
                        try { _reportBytesReceived(totalLen); } catch { }

                        // 3) Wrap as BufferLease — ownership transfers to subscriber.
                        //    CONTRACT: the subscriber (wrapper in ReliableClientSubscriptions)
                        //    is solely responsible for calling lease.Dispose() exactly once.
                        //    FRAME_READER must NOT touch the lease after this point.
                        BufferLease lease = BufferLease.TakeOwnership(
                            rented, ReliableClient.HeaderSize, payloadLen);
                        ownershipTransferred = true;

                        // 4) Deliver to subscriber — catch exceptions to protect the loop.
                        //    Do NOT dispose lease here under any circumstance; the subscriber owns it.
                        try
                        {
                            _onMessage(lease);
                        }
                        catch (System.Exception handlerEx)
                        {
                            // Subscriber faulted — log and continue the receive loop.
                            // The lease is already disposed by the subscriber's finally/using block.
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Error($"[SDK.{nameof(FRAME_READER)}] handler-faulted—loop continues. msg={handlerEx.Message}", handlerEx);
                        }
                    }
                    catch
                    {
                        // Only return the raw buffer if the lease was never created.
                        // Once ownershipTransferred == true, BufferLease owns the buffer.
                        if (!ownershipTransferred)
                        {
                            try { _bufferPool.Return(rented); } catch { }
                        }

                        throw;
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<System.Byte>.Shared.Return(headerBuffer);
                }
            }
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown — not an error.
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(FRAME_READER)}:{nameof(ReceiveLoopAsync)}] faulted msg={ex.Message}", ex);

            _onError(ex);
        }
    }


    private static async System.Threading.Tasks.Task RECEIVE_EXACTLY_ASYNC(System.Net.Sockets.Socket s, System.Memory<System.Byte> dst, System.Threading.CancellationToken token)
    {
        System.Int32 read = 0;
        while (read < dst.Length)
        {
            System.Int32 n = await s.ReceiveAsync(dst[read..], System.Net.Sockets.SocketFlags.None, token).ConfigureAwait(false);
            if (n == 0)
            {
                throw new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.ConnectionReset);
            }

            read += n;
        }
    }
}