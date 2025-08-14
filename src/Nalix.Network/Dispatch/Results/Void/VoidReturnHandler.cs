// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Core.Context;

namespace Nalix.Network.Dispatch.Results.Void;

/// <inheritdoc/>
internal sealed class VoidReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context) => System.Threading.Tasks.ValueTask.CompletedTask;
}