// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Memory;

/// <inheritdoc/>
internal sealed class MemoryReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Memory<System.Byte> memory)
        {
            _ = await context.Connection.Tcp.SendAsync(memory);
        }
    }
}