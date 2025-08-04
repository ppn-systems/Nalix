using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Protocols.Messages;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Dispatch.ReturnTypes.Primitives;

/// <inheritdoc/>
internal sealed class StringReturnHandler<TPacket> : IReturnHandler<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.String data)
        {
            LiteralPacket text = ObjectPoolManager.Instance.Get<LiteralPacket>();
            try
            {
                text.Initialize(data);
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return<LiteralPacket>(text);
            }
        }
    }
}