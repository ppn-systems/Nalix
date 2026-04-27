// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Runtime.Internal.Results.Memory;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class MemoryReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is not Memory<byte> memory)
        {
            return;
        }

        if (context.IsReliable)
        {
            await context.Connection.TCP.SendAsync(memory).ConfigureAwait(false);
        }
        else
        {
            await context.Connection.UDP.SendAsync(memory).ConfigureAwait(false);
        }
    }
}
