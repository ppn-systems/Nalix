using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Task;

/// <inheritdoc/>
internal sealed class TaskReturnHandler<TPacket, TResult>(IReturnHandler<TPacket> innerHandler)
    : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Threading.Tasks.Task<TResult> task)
        {
            try
            {
                var taskResult = await task;
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