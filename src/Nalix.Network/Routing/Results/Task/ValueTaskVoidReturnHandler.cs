// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Routing.Results.Task;

/// <inheritdoc/>
internal sealed class ValueTaskVoidReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context)
    {
        if (result is not System.Threading.Tasks.ValueTask valueTask)
        {
            return;
        }

        await valueTask.ConfigureAwait(false);
    }
}
