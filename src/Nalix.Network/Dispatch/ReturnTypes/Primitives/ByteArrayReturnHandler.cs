using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Primitives;

/// <inheritdoc/>
internal sealed class ByteArrayReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Byte[] data)
        {
            _ = await context.Connection.Tcp.SendAsync(data);
        }
    }
}