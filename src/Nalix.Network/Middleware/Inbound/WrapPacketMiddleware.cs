// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection.Protocols;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Models;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch;
using Nalix.Network.Dispatch.Catalog;
using Nalix.Shared.Injection;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that wraps a packet with compression and encryption as needed before dispatch.
/// </summary>
[PacketMiddleware(MiddlewareStage.Inbound, order: 2, name: "Wrap")]
public class WrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        IPacket current = context.Packet;

        System.Boolean needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;
        System.Boolean needCompress = ShouldCompress(context);

        if (!needEncrypt && !needCompress)
        {
            await next();
            return;
        }

        try
        {
            PacketCatalog? catalog = InstanceManager.Instance.GetExistingInstance<PacketCatalog>();
            if (catalog is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(WrapPacketMiddleware)}] Missing PacketCatalog.");
                return;
            }

            if (catalog.TryGetTransformer(current.GetType(), out PacketTransformer t))
            {
                if (needCompress)
                {
                    current = t.Compress(current);
                }

                if (needEncrypt)
                {
                    current = t.Encrypt(
                        current,
                        context.Connection.EncryptionKey,
                        context.Connection.Encryption);
                }

                if (!ReferenceEquals(current, context.Packet))
                {
                    context.SetPacket(current);
                }
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(WrapPacketMiddleware)}] " +
                                               $"No transformer found for packet type {current.GetType().Name}.");

                _ = await context.Connection.Tcp.SendAsync("Unsupported packet type for encryption/compression.")
                                                .ConfigureAwait(false);
            }

        }
        catch (System.Exception)
        {
            _ = await context.Connection.Tcp.SendAsync("An error occurred while processing your request.")
                                            .ConfigureAwait(false);
        }

        await next();
    }

    private static System.Boolean ShouldCompress(in PacketContext<IPacket> context)
    {
        return context.Packet.Transport == TransportProtocol.Tcp
            ? context.Packet.Length - PacketConstants.CompressionThreshold > PacketConstants.CompressionThreshold
            : context.Packet.Transport == TransportProtocol.Udp &&
              context.Packet.Length - PacketConstants.CompressionThreshold is > 600 and < 1200;
    }
}
