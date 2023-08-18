using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Task;

/// <inheritdoc/>
internal sealed class ValueTaskReturnHandler<TPacket, TResult>(IReturnHandler<TPacket> innerHandler)
    : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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