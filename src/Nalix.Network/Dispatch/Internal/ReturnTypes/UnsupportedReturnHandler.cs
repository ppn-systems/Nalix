using Nalix.Network.Dispatch.Core;

namespace Nalix.Network.Dispatch.Internal.ReturnTypes;

/// <inheritdoc/>
internal sealed class UnsupportedReturnHandler<TPacket>(System.Type returnType) : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        // Log warning về unsupported type
        context.SetProperty("UnsupportedReturnType", returnType.Name);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}