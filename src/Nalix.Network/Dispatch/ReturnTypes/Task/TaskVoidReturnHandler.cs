using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.ReturnTypes.Task;

/// <inheritdoc/>
internal sealed class TaskVoidReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Threading.Tasks.Task task)
        {
            await task;
        }
    }
}