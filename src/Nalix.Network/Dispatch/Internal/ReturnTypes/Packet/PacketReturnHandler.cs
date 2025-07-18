using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Internal.ReturnTypes.Packet;

/// <inheritdoc/>
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket>
    where TPacket : Common.Package.IPacket
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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