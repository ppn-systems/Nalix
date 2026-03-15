// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;

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
    System.Action<BufferLease> onMessage,        // ← đổi IBufferLease → BufferLease (concrete)
    System.Action<System.Exception> onError,
    System.Action<System.Int32> reportBytesReceived)
{
    private readonly BufferPoolManager _bufferPool =
        InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

    private readonly TransportOptions _options =
        options ?? throw new System.ArgumentNullException(nameof(options));

    private readonly System.Func<System.Net.Sockets.Socket> _getSocket =
        getSocket ?? throw new System.ArgumentNullException(nameof(getSocket));

    private readonly System.Action<System.Exception> _onError =
        onError ?? throw new System.ArgumentNullException(nameof(onError));

    // Concrete type BufferLease — HandleReceiveMessage cần gọi CopyFrom(lease.Span)
    // mà không cần cast IBufferLease → BufferLease ở mỗi lần invoke.
    private readonly System.Action<BufferLease> _onMessage =
        onMessage ?? throw new System.ArgumentNullException(nameof(onMessage));

    private readonly System.Action<System.Int32> _reportBytesReceived =
        reportBytesReceived ?? throw new System.ArgumentNullException(nameof(reportBytesReceived));

    public async System.Threading.Tasks.Task ReceiveLoopAsync(
        System.Threading.CancellationToken token)
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
                // 1) Đọc 2-byte length header
                System.Byte[] headerBuffer =
                    System.Buffers.ArrayPool<System.Byte>.Shared.Rent(ReliableClient.HeaderSize);
                try
                {
                    var headerMemory = new System.Memory<System.Byte>(
                        headerBuffer, 0, ReliableClient.HeaderSize);

                    await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);

                    System.UInt16 totalLen =
                        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                            headerMemory.Span);

                    if (totalLen < ReliableClient.HeaderSize || totalLen > _options.MaxPacketSize)
                    {
                        throw new System.Net.Sockets.SocketException(
                            (System.Int32)System.Net.Sockets.SocketError.ProtocolNotSupported);
                    }

                    System.Int32 payloadLen = totalLen - ReliableClient.HeaderSize;

                    // 2) Rent buffer cho full frame, đọc payload
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

                        // Best-effort telemetry — không bao giờ throw
                        try { _reportBytesReceived(totalLen); } catch { }

                        // 3) Bọc thành BufferLease — ownership chuyển sang _onMessage.
                        //    CONTRACT: HandleReceiveMessage (= _onMessage) là SOLE OWNER.
                        //    Nó sẽ:
                        //      a) Tạo copy riêng cho từng sync subscriber qua CopyFrom()
                        //      b) Dispose lease gốc trong finally của chính nó
                        //    FRAME_READER không được đụng vào lease sau điểm này.
                        BufferLease lease = BufferLease.TakeOwnership(
                            rented, ReliableClient.HeaderSize, payloadLen);
                        ownershipTransferred = true;

                        // 4) Deliver — bắt exception để bảo vệ receive loop.
                        //    KHÔNG dispose lease ở đây — HandleReceiveMessage chịu trách nhiệm.
                        try
                        {
                            _onMessage(lease);
                        }
                        catch (System.Exception handlerEx)
                        {
                            // Handler faulted sau khi ownershipTransferred = true.
                            // HandleReceiveMessage có finally { lease.Dispose(); }
                            // nên buffer đã được trả về pool — log và tiếp tục loop.
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error(
                                    $"[SDK.{nameof(FRAME_READER)}] handler-faulted—loop continues." +
                                    $" msg={handlerEx.Message}", handlerEx);
                        }
                    }
                    catch
                    {
                        // Chỉ return raw buffer nếu lease CHƯA được tạo.
                        // Một khi ownershipTransferred == true, BufferLease sở hữu buffer.
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
            // Shutdown bình thường — không phải lỗi.
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error(
                    $"[SDK.{nameof(FRAME_READER)}:{nameof(ReceiveLoopAsync)}]" +
                    $" faulted msg={ex.Message}", ex);

            _onError(ex);
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
}