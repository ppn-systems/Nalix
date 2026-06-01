// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;

namespace Nalix.Runtime.Internal.Results.Packet;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is not IPacket packet)
        {
            return;
        }

        try
        {
            await context.Sender.SendAsync(packet).ConfigureAwait(false);
        }
        finally
        {
            (packet as IDisposable)?.Dispose();
        }
    }
}
