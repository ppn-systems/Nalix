﻿using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Memory;

/// <inheritdoc/>
internal sealed class ReadOnlyMemoryReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.ReadOnlyMemory<System.Byte> memory)
        {
            _ = await context.Connection.Tcp.SendAsync(memory);
        }
    }
}