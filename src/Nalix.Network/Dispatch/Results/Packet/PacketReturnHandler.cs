// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;

namespace Nalix.Network.Dispatch.Results.Packet;

/// <inheritdoc/>
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket>
    where TPacket : IPacket
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.DisallowNull] PacketContext<TPacket> context)
    {
        if (result is not TPacket packet)
        {
            return;
        }

        _ = await context.Connection.TCP.SendAsync(packet
                                        .Serialize())
                                        .ConfigureAwait(false);
    }
}