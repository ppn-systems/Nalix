// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Runtime.Dispatching;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Runtime.Internal.Results.Packet;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is not IPacket packet)
        {
            return;
        }

        if (packet.Flags.HasFlag(PacketFlags.RELIABLE))
        {
            await context.Sender.SendAsync(packet).ConfigureAwait(false);
            return;
        }

        if (packet.Flags.HasFlag(PacketFlags.UNRELIABLE))
        {
            await context.Connection.UDP.SendAsync(packet).ConfigureAwait(false);
        }
    }
}
