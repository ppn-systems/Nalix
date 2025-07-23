using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.ReturnTypes;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.ReturnTypes.Void;

/// <inheritdoc/>
internal sealed class VoidReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context) => System.Threading.Tasks.ValueTask.CompletedTask;
}