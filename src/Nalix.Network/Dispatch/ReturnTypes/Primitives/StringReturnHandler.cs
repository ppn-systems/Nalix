﻿using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;

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
            _ = await context.Connection.Tcp.SendAsync(TPacket.Create(0, data).Serialize());
        }
    }
}