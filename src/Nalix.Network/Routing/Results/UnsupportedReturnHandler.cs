// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing.Results;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class UnsupportedReturnHandler<TPacket>(Type returnType) : IReturnHandler<TPacket> where TPacket : IPacket
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> s_loggedTypes = new();

    /// <inheritdoc/>
    public ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (s_loggedTypes.TryAdd(returnType, true))
        {
            throw new NotSupportedException($"unsupported return type: {returnType.FullName}");
        }

        return ValueTask.CompletedTask;
    }
}
