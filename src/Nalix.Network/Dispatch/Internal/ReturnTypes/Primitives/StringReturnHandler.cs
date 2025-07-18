﻿using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Internal.ReturnTypes.Primitives;

/// <inheritdoc/>
internal sealed class StringReturnHandler<TPacket> : IReturnHandler<TPacket>
    where TPacket : Common.Package.IPacket, Common.Package.IPacketFactory<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.String data)
        {
            using var packet = TPacket.Create(0, data);
            _ = await context.Connection.Tcp.SendAsync(packet.Serialize());
        }
    }
}