// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing.Results.Task;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class TaskReturnHandler<TPacket, TResult>(IReturnHandler<TPacket> innerHandler) : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        [AllowNull] object result,
        PacketContext<TPacket> context)
    {
        if (result is not Task<TResult> task)
        {
            return;
        }

        TResult taskResult = await task.ConfigureAwait(false);
        await innerHandler.HandleAsync(taskResult, context)
                          .ConfigureAwait(false);
    }
}
