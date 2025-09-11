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
        if (result is not System.Byte[] data)
        {
            return;
        }

        if (data.Length == 0)
        {
            return;
        }

        _ = await context.Connection.TCP.SendAsync(data)
                                        .ConfigureAwait(false);
    }
}