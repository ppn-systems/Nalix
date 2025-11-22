// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Results.Memory;

/// <inheritdoc/>
internal sealed class MemoryReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.DisallowNull] PacketContext<TPacket> context)
    {
        if (result is not System.Memory<System.Byte> memory)
        {
            return;
        }

        _ = await context.Connection.TCP.SendAsync(memory)
                                        .ConfigureAwait(false);
    }
}