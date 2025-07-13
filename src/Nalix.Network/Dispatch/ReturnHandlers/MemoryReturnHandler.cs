using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.ReturnHandlers;

/// <inheritdoc/>
public sealed class MemoryReturnHandler<TPacket> : IPacketReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Memory<System.Byte> memory)
        {
            await context.Connection.Tcp.SendAsync(memory);
        }
    }
}