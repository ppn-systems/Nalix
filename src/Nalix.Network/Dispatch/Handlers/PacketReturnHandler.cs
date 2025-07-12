using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Handlers;

/// <inheritdoc/>
public sealed class PacketReturnHandler<TPacket> : IReturnTypeHandler<TPacket>
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
            await context.Connection.Tcp.SendAsync(packet.Serialize());
        }
    }
}