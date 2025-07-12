using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Handlers;

/// <inheritdoc/>
public sealed class VoidReturnHandler<TPacket> : IReturnTypeHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}