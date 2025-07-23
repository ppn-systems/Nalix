using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch.Middleware;

internal class PacketTransformMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        try
        {
            TPacket current = context.Packet;

            if (context.Packet.Flags.HasFlag<PacketFlags>(PacketFlags.Encrypted))
            {
                current = TPacket.Decrypt(context.Packet, context.Connection.EncryptionKey, context.Connection.Encryption);
            }

            if (context.Packet.Flags.HasFlag<PacketFlags>(PacketFlags.Compressed))
            {
                current = TPacket.Decompress(context.Packet);
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
}