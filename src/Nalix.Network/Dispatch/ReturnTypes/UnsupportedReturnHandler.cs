using Nalix.Common.Logging;
using Nalix.Network.Dispatch.Core;
using Nalix.Shared.Injection;

namespace Nalix.Network.Dispatch.ReturnTypes;

/// <inheritdoc/>
internal sealed class UnsupportedReturnHandler<TPacket>(System.Type returnType) : IReturnHandler<TPacket>
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Boolean> _loggedTypes = new();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (_loggedTypes.TryAdd(returnType, true))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn(
                $"[Dispatch] Unsupported return type '{returnType.Name}' encountered. " +
                "Result will not be processed, but stored in context properties.");
        }

        context.SetProperty("UnsupportedReturnType", returnType.Name);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}