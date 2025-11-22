// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Results.Task;

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