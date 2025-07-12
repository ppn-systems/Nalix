using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Handlers;

/// <inheritdoc/>
public sealed class ValueTaskReturnHandler<TPacket, TResult>(IReturnTypeHandler<TPacket> innerHandler)
    : IReturnTypeHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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