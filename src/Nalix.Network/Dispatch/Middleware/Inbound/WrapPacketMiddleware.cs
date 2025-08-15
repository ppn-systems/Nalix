// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core.Context;
using Nalix.Network.Dispatch.Middleware.Core.Attributes;
using Nalix.Network.Dispatch.Middleware.Core.Enums;
using Nalix.Network.Dispatch.Middleware.Core.Interfaces;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Text;
using static Nalix.Network.Dispatch.Inspection.PacketRegistry;

namespace Nalix.Network.Dispatch.Middleware.Inbound;

/// <summary>
/// Middleware that wraps a packet with compression and encryption as needed before dispatch.
/// </summary>
[PacketMiddleware(MiddlewareStage.PostDispatch, order: 2, name: "Wrap")]
public class WrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        try
        {
            IPacket current = context.Packet;

            if (TryResolveTransformer(current.GetType(), out PacketTransformerDelegates? t) && t is not null)
            {
                if (ShouldCompress(context))
                {
                    current = t.Compress(current);
                }

                if (context.Attributes.Encryption?.IsEncrypted ?? false)
                {
                    current = t.Encrypt(
                        current,
                        context.Connection.EncryptionKey,
                        context.Connection.Encryption);
                }
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.SetPacket(current);
            }
        }
        catch (System.Exception)
        {
            Text256 text = ObjectPoolManager.Instance.Get<Text256>();
            try
            {
                text.Initialize("An error occurred while processing your request.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return(text);
            }
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
