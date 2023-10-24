// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Results;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging;

namespace Nalix.Network.Dispatch.Results.Primitives;

/// <inheritdoc/>
internal sealed class StringReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        System.Object? result,
        PacketContext<TPacket> context)
    {
        if (result is System.String data)
        {
            TextPacket text = ObjectPoolManager.Instance.Get<TextPacket>();
            try
            {
                text.Initialize(data);
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return(text);
            }
        }
    }
}