// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;

namespace Nalix.Runtime.Internal.Results.Task;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class TaskVoidReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is not System.Threading.Tasks.Task task)
        {
            return;
        }

        await task.ConfigureAwait(false);
    }
}

