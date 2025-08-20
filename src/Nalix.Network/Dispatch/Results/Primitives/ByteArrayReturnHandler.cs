// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Results.Primitives;

/// <inheritdoc/>
internal sealed class ByteArrayReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Byte[] data)
        {
            _ = await context.Connection.Tcp.SendAsync(data);
        }
    }
}