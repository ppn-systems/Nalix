using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.ReturnTypes;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.ReturnTypes.Task;

/// <inheritdoc/>
internal sealed class ValueTaskVoidReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.Threading.Tasks.ValueTask valueTask)
        {
            await valueTask;
        }
    }
}