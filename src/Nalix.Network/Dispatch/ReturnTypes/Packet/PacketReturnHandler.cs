using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Packet;

/// <inheritdoc/>
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket>
    where TPacket : IPacket
{
    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is TPacket packet)
        {
            _ = await context.Connection.Tcp.SendAsync(packet.Serialize());
        }
    }
}