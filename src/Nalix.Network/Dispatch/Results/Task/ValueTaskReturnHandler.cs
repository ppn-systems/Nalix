// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Results.Task;

/// <inheritdoc/>
internal sealed class ValueTaskReturnHandler<TPacket, TResult>(IReturnHandler<TPacket> innerHandler)
    : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Threading.Tasks.ValueTask<TResult> valueTask)
        {
            try
            {
                var taskResult = await valueTask;
                await innerHandler.HandleAsync(taskResult, context);
            }
            catch (System.Exception ex)
            {
                context.SetProperty("HandlerException", ex);
                throw;
            }
        }
    }
}