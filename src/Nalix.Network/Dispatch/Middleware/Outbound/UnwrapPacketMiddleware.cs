// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core.Context;
using Nalix.Network.Dispatch.Middleware.Core.Attributes;
using Nalix.Network.Dispatch.Middleware.Core.Enums;
using Nalix.Network.Dispatch.Middleware.Core.Interfaces;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Text;
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
        IPacket current = context.Packet;

        System.Boolean needDecrypt = current.Flags.HasFlag(PacketFlags.Encrypted);
        System.Boolean needDecompress = current.Flags.HasFlag(PacketFlags.Compressed);

        if (!needDecrypt && !needDecompress)
        {
            await next();
            return;
        }

        try
        {
            if (TryResolveTransformer(current.GetType(), out PacketTransformerDelegates? t) && t is not null)
            {
                if (needDecrypt)
                {
                    current = t.Decrypt(
                        current,
                        context.Connection.EncryptionKey,
                        context.Connection.Encryption);
                }

                if (needDecompress)
                {
                    current = t.Decompress(current);
                }

                if (!ReferenceEquals(current, context.Packet))
                {
                    context.SetPacket(current);
                }
            }
            else
            {
                Text256 text = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                   .Get<Text256>();
                try
                {
                    text.Initialize("Unsupported packet type for decryption/decompression.");
                    _ = await context.Connection.Tcp.SendAsync(text.Serialize());
                    return;
                }
                finally
                {
                    InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                            .Return(text);
                }
            }
        }
        catch (System.Exception)
        {
            Text256 text = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                               .Get<Text256>();
            try
            {
                text.Initialize("Packet transform failed.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());
                return;
            }
            finally
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return(text);
            }
        }

        await next();
    }
}