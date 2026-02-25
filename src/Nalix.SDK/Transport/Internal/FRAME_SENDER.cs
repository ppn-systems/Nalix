// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.SDK.Transport.Internal;

internal sealed class FRAME_SENDER(
    System.Func<System.Net.Sockets.Socket> getSocket,
    TransportOptions options,
    System.Action<System.Int32> reportBytesSent,
    System.Action<System.Exception> onError)
{
    private readonly BufferPoolManager _bufferPool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

    private readonly TransportOptions _options = options ?? throw new System.ArgumentNullException(nameof(options));
    private readonly System.Action<System.Exception> _onError = onError ?? throw new System.ArgumentNullException(nameof(onError));
    private readonly System.Func<System.Net.Sockets.Socket> _getSocket = getSocket ?? throw new System.ArgumentNullException(nameof(getSocket));
    private readonly System.Action<System.Int32> _reportBytesSent = reportBytesSent ?? throw new System.ArgumentNullException(nameof(reportBytesSent));

    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken cancellationToken = default)
    {
        if (payload.Length > _options.MaxPacketSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(payload), "Payload exceeds MaxPacketSize.");
        }

        System.Net.Sockets.Socket s;
        try
        {
            s = _getSocket();
        }
        catch (System.Exception ex)
        {
            _onError(ex);
            return false;
        }

        System.Int32 totalLen = ReliableClient.HeaderSize + payload.Length;

        try
        {
            if (totalLen <= 1024)
            {
                System.Byte[] buf = new System.Byte[ReliableClient.HeaderSize + payload.Length];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                      .AsSpan(buf, 0, ReliableClient.HeaderSize), (System.UInt16)totalLen);

                payload.Span.CopyTo(System.MemoryExtensions.AsSpan(buf, ReliableClient.HeaderSize));

                System.Int32 sent = 0;
                while (sent < buf.Length)
                {
                    System.Int32 n = await s.SendAsync(new System.ReadOnlyMemory<System.Byte>(buf, sent, buf.Length - sent), System.Net.Sockets.SocketFlags.None, cancellationToken)
                                            .ConfigureAwait(false);

                    if (n == 0)
                    {
                        throw new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.ConnectionReset);
                    }

                    sent += n;
                }

                _reportBytesSent(buf.Length);
                return true;
            }
            else
            {
                System.Byte[] rent = _bufferPool.Rent(totalLen);
                try
                {
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                          .AsSpan(rent, 0, ReliableClient.HeaderSize), (System.UInt16)totalLen);

                    payload.Span.CopyTo(System.MemoryExtensions.AsSpan(rent, ReliableClient.HeaderSize, payload.Length));

                    System.Int32 sent = 0;
                    while (sent < totalLen)
                    {
                        System.Int32 n = await s.SendAsync(new System.ReadOnlyMemory<System.Byte>(rent, sent, totalLen - sent), System.Net.Sockets.SocketFlags.None, cancellationToken)
                                                .ConfigureAwait(false);
                        if (n == 0)
                        {
                            throw new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.ConnectionReset);
                        }

                        sent += n;
                    }

                    _reportBytesSent(totalLen);
                    return true;
                }
                finally
                {
                    _bufferPool.Return(rent);
                }
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(FRAME_SENDER)}:{nameof(SendAsync)}] send-error msg={ex.Message}", ex);

            _onError(ex);
            return false;
        }
    }

    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(IPacket packet, System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);

        if (packet.Length == 0)
        {
            return await SendAsync(System.ReadOnlyMemory<System.Byte>.Empty, cancellationToken);
        }

        if (packet.Length < 512)
        {
            System.Byte[] tmp = new System.Byte[packet.Length];
            System.Int32 w = packet.Serialize(tmp);
            return await SendAsync(new System.ReadOnlyMemory<System.Byte>(tmp, 0, w), cancellationToken);
        }
        else
        {
            System.Byte[] rent = _bufferPool.Rent(packet.Length);
            try
            {
                System.Int32 w = packet.Serialize(rent);
                return await SendAsync(new System.ReadOnlyMemory<System.Byte>(rent, 0, w), cancellationToken);
            }
            finally
            {
                _bufferPool.Return(rent);
            }
        }
    }
}