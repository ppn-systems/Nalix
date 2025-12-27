// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Routing.Results;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class UnsupportedReturnHandler<TPacket>(Type returnType) : IReturnHandler<TPacket> where TPacket : IPacket
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> _loggedTypes = new();

    /// <inheritdoc/>
    public ValueTask HandleAsync(
        [AllowNull] object result,
        PacketContext<TPacket> context)
    {
        if (_loggedTypes.TryAdd(returnType, true))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(UnsupportedReturnHandler<>)}:{nameof(HandleAsync)}] unsupported-return type={returnType.Name}");
        }

        return ValueTask.CompletedTask;
    }
}
