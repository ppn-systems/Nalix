// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Dispatch.Results;

/// <inheritdoc/>
internal sealed class UnsupportedReturnHandler<TPacket>(System.Type returnType) : IReturnHandler<TPacket>
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Boolean> _loggedTypes = new();

    /// <inheritdoc/>
    public System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context)
    {
        if (_loggedTypes.TryAdd(returnType, true))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(UnsupportedReturnHandler<>)}:{nameof(HandleAsync)}] unsupported-return type={returnType.Name}");
        }

        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}