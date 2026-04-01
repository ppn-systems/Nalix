// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Network.Routing;

namespace Nalix.Network.Internal.Results.Primitives;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class ByteArrayReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is not byte[] data)
        {
            return;
        }

        ProtocolType protocol = context.Packet.Protocol;

        if (protocol == ProtocolType.TCP)
        {
            await context.Connection.TCP.SendAsync(data).ConfigureAwait(false);
            return;
        }

        if (protocol == ProtocolType.UDP)
        {
            await context.Connection.UDP.SendAsync(data).ConfigureAwait(false);
        }
    }
}
