using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Handlers;

/// <inheritdoc/>
public sealed class ByteArrayReturnHandler<TPacket> : IReturnTypeHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Byte[] data)
        {
            await context.Connection.Tcp.SendAsync(data);
        }
    }
}