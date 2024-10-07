using Nalix.Network.Dispatch.Core;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Dispatch.Internal.ReturnTypes;

/// <inheritdoc/>
internal sealed class UnsupportedReturnHandler<TPacket>(System.Type returnType) : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        // Log warning về unsupported type
        context.SetProperty("UnsupportedReturnType", returnType.Name);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}