using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.ReturnHandlers;

/// <inheritdoc/>
public sealed class TaskReturnHandler<TPacket, TResult>(IPacketReturnHandler<TPacket> innerHandler)
    : IPacketReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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