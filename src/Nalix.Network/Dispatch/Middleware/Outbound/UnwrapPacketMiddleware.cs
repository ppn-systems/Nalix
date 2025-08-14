// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core.Context;
using Nalix.Network.Dispatch.Middleware.Core.Attributes;
using Nalix.Network.Dispatch.Middleware.Core.Enums;
using Nalix.Network.Dispatch.Middleware.Core.Interfaces;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging;
using static Nalix.Network.Dispatch.Inspection.PacketRegistry;

namespace Nalix.Network.Dispatch.Middleware.Outbound;

/// <summary>
/// Middleware that unwraps (decrypts and/or decompresses) packets before further processing.
/// </summary>
[PacketMiddleware(MiddlewareStage.PreDispatch, order: 3, name: "Unwrap")]
public class UnwrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        try
        {
            IPacket current = context.Packet;

            if (TryResolveTransformer(current.GetType(), out PacketTransformerDelegates? t) && t is not null)
            {
                if (context.Packet.Flags.HasFlag(PacketFlags.Encrypted))
                {
                    current = t.Decrypt(
                        current,
                        context.Connection.EncryptionKey,
                        context.Connection.Encryption);
                }

                if (context.Packet.Flags.HasFlag(PacketFlags.Compressed))
                {
                    current = t.Decompress(current);
                }
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.SetPacket(current);
            }
        }
        catch (System.Exception)
        {
            TextPacket text = ObjectPoolManager.Instance.Get<TextPacket>();
            try
            {
                text.Initialize($"Packet transform failed.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return(text);
            }
        }

        await next();
    }
}