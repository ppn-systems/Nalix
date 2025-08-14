// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core.Context;

namespace Nalix.Network.Dispatch.Results.Packet;

/// <inheritdoc/>
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket>
    where TPacket : IPacket
{
    /// <inheritdoc/>
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