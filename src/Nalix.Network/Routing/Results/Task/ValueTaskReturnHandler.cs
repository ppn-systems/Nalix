// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Network.Routing;
using Nalix.Network.Routing.Results;

namespace Nalix.Network.Routing.Results.Task;

/// <inheritdoc/>
internal sealed class ValueTaskReturnHandler<TPacket, TResult>(IReturnHandler<TPacket> innerHandler) : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context)
    {
        if (result is not System.Threading.Tasks.ValueTask<TResult> valueTask)
        {
            return;
        }

        TResult taskResult = await valueTask.ConfigureAwait(false);
        await innerHandler.HandleAsync(taskResult, context)
                          .ConfigureAwait(false);
    }
}
