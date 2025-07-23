using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;

namespace Nalix.Network.Dispatch.Middleware;

internal class DecompressMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketCompressor<TPacket>, IPacketFactory<TPacket>
{
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        if (context.Packet.IsCompression)
        {
            try
            {
                context.SetPacket(TPacket.Decompress(context.Packet));
            }
            catch (System.Exception)
            {
                _ = await context.Connection.Tcp.SendAsync(TPacket.Create(0, "Packet decompress failed!"));
                return;
            }
        }

        await next();
    }
}