// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;

namespace Nalix.Network.Routing.Results.Packet;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is not TPacket packet)
        {
            return;
        }

        if (packet.Protocol == ProtocolType.TCP)
        {
            await context.Connection.TCP.SendAsync(packet).ConfigureAwait(false);
            return;
        }

        if (packet.Protocol == ProtocolType.UDP)
        {
            await context.Connection.UDP.SendAsync(packet).ConfigureAwait(false);
        }
    }
}
