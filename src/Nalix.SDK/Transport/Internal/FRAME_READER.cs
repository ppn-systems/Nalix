// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.SDK.Transport.Internal;

internal sealed class FRAME_READER(
    System.Func<Socket> getSocket,
    TransportOptions options,
    System.Action<IBufferLease> onMessage,
    System.Action<System.Exception> onError,
    System.Action<System.Int32> reportBytesReceived)
{
    private readonly BufferPoolManager _bufferPool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

    private readonly TransportOptions _options = options ?? throw new System.ArgumentNullException(nameof(options));
    private readonly System.Func<Socket> _getSocket = getSocket ?? throw new System.ArgumentNullException(nameof(getSocket));
    private readonly System.Action<System.Exception> _onError = onError ?? throw new System.ArgumentNullException(nameof(onError));
    private readonly System.Action<IBufferLease> _onMessage = onMessage ?? throw new System.ArgumentNullException(nameof(onMessage));
    private readonly System.Action<System.Int32> _reportBytesReceived = reportBytesReceived ?? throw new System.ArgumentNullException(nameof(reportBytesReceived));

    public async Task ReceiveLoopAsync(CancellationToken token)
    {
        Socket s;
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
                // 1) Read header
                System.Byte[] headerBuffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(ReliableClient.HeaderSize);
                try
                {
                    var headerMemory = new System.Memory<System.Byte>(headerBuffer, 0, ReliableClient.HeaderSize);
                    await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);
                    System.UInt16 totalLen = BinaryPrimitives.ReadUInt16LittleEndian(headerMemory.Span);

                    if (totalLen < ReliableClient.HeaderSize || totalLen > _options.MaxPacketSize)
                    {
                        throw new SocketException((System.Int32)SocketError.ProtocolNotSupported);
                    }

                    System.Int32 payloadLen = totalLen - ReliableClient.HeaderSize;

                    // 2) Rent buffer for header+payload and read payload into buffer[HeaderSize..]
                    System.Byte[] rented = _bufferPool.Rent(totalLen);
                    System.Boolean ownershipTransferred = false;
                    try
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions.AsSpan(rented, 0, ReliableClient.HeaderSize), totalLen);

                        if (payloadLen > 0)
                        {
                            await RECEIVE_EXACTLY_ASYNC(s, System.MemoryExtensions.AsMemory(rented, ReliableClient.HeaderSize, payloadLen), token).ConfigureAwait(false);
                        }

                        // 2.1) Report number of bytes received (header + payload)
                        try
                        {
                            _reportBytesReceived(totalLen);
                        }
                        catch { /* best-effort reporting */ }

                        // 3) Wrap as BufferLease starting at HeaderSize (payload slice)
                        BufferLease lease = BufferLease.TakeOwnership(rented, ReliableClient.HeaderSize, payloadLen);
                        ownershipTransferred = true;

                        try
                        {
                            _onMessage(lease);
                        }
                        catch (System.Exception)
                        {
                            // Ensure we free lease on handler exception
                            try { lease.Dispose(); } catch { }
                            throw;
                        }

                        // Responsibility to Dispose lease remains with handlers.
                    }
                    catch
                    {
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
            // normal cancellation
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(FRAME_READER)}:{nameof(ReceiveLoopAsync)}] faulted msg={ex.Message}", ex);

            _onError(ex);
        }
    }

    private static async Task RECEIVE_EXACTLY_ASYNC(Socket s, System.Memory<System.Byte> dst, CancellationToken token)
    {
        System.Int32 read = 0;
        while (read < dst.Length)
        {
            System.Int32 n = await s.ReceiveAsync(dst[read..], SocketFlags.None, token).ConfigureAwait(false);
            if (n == 0)
            {
                throw new SocketException((System.Int32)SocketError.ConnectionReset);
            }

            read += n;
        }
    }
}