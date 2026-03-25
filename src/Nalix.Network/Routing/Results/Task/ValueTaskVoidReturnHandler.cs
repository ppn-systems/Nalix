// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing.Results.Task;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class ValueTaskVoidReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        object? result,
        PacketContext<TPacket> context)
    {
        if (result is not ValueTask valueTask)
        {
            return;
        }

        await valueTask.ConfigureAwait(false);
    }
}
