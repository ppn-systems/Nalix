// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Results.Task;

/// <inheritdoc/>
internal sealed class ValueTaskReturnHandler<TPacket, TResult>(IReturnHandler<TPacket> innerHandler) : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is not System.Threading.Tasks.ValueTask<TResult> valueTask)
        {
            return;
        }

        try
        {
            TResult taskResult = await valueTask.ConfigureAwait(false);
            await innerHandler.HandleAsync(taskResult, context)
                              .ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            context.SetProperty("HandlerException", ex);
            throw;
        }
    }
}