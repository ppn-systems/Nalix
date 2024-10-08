using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Internal.ReturnTypes.Memory;

/// <inheritdoc/>
internal sealed class MemoryReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Memory<System.Byte> memory)
        {
            _ = await context.Connection.Tcp.SendAsync(memory);
        }
    }
}