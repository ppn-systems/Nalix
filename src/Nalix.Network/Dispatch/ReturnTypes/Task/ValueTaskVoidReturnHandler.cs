// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Task;

/// <inheritdoc/>
internal sealed class ValueTaskVoidReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Threading.Tasks.ValueTask valueTask)
        {
            await valueTask;
        }
    }
}