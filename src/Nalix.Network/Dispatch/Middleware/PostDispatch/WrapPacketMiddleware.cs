using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Transport;

namespace Nalix.Network.Dispatch.Middleware.Post;

/// <summary>
/// Middleware that wraps a packet with compression and encryption as needed before dispatch.
/// </summary>
/// <typeparam name="TPacket">
/// The type of packet, which must implement <see cref="IPacket"/> and <see cref="IPacketTransformer{TPacket}"/>.
/// </typeparam>
[PacketMiddleware(MiddlewareStage.PostDispatch, order: 2, name: "Wrap")]
public class WrapPacketMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        try
        {
            TPacket current = context.Packet;

            if (WrapPacketMiddleware<TPacket>.ShouldCompress(context))
            {
                current = TPacket.Compress(current);
            }

            if (context.Attributes.Encryption?.IsEncrypted ?? false)
            {
                current = TPacket.Encrypt(
                    current,
                    context.Connection.EncryptionKey,
                    context.Connection.Encryption);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.SetPacket(current);
            }
        }
        catch (System.Exception)
        {
            LiteralPacket text = ObjectPoolManager.Instance.Get<LiteralPacket>();
            try
            {
                text.Initialize("An error occurred while processing your request.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return<LiteralPacket>(text);
            }
        }

        await next();
    }

    private static System.Boolean ShouldCompress(in PacketContext<TPacket> context)
    {
        return (context.Packet.Transport == TransportProtocol.Tcp)
             ? (context.Packet.Length - PacketConstants.CompressionThreshold) > PacketConstants.CompressionThreshold
             : (context.Packet.Transport == TransportProtocol.Udp) &&
               (context.Packet.Length - PacketConstants.CompressionThreshold) is > 600 and < 1200;
    }
}
