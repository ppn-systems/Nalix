using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;

namespace Nalix.Network.Dispatch.Middleware.Post;
internal class WrapPacketMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
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
        catch (System.Exception ex)
        {
            _ = await context.Connection.Tcp.SendAsync(
                TPacket.Create(0, "Packet transform failed: " + ex.Message));

            return;
        }

        await next();
    }

    private static System.Boolean ShouldCompress(in PacketContext<TPacket> context)
    {
        System.Int32 length = context.Packet.Length;

        return context.Packet.Transport == TransportProtocol.Tcp
            ? length > 1500
            : context.Packet.Transport == TransportProtocol.Udp && length > 600 && length < 1200;
    }
}
