// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Models;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch;
using Nalix.Network.Dispatch.Catalog;
using Nalix.Shared.Injection;

namespace Nalix.Network.Middleware.Outbound;

/// <summary>
/// Middleware that unwraps (decrypts and/or decompresses) packets before further processing.
/// </summary>
[PacketMiddleware(MiddlewareStage.Outbound, order: 3, name: "Unwrap")]
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
            PacketCatalog? catalog = InstanceManager.Instance.GetExistingInstance<PacketCatalog>();
            if (catalog is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(UnwrapPacketMiddleware)}] Missing PacketCatalog." +
                                               $"OpCode={context.Attributes.OpCode}, From={context.Connection.RemoteEndPoint}");
                return;
            }

            if (catalog.TryGetTransformer(current.GetType(), out PacketTransformer t))
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
                _ = await context.Connection.Tcp.SendAsync("Unsupported packet type for decryption/decompression.")
                                                .ConfigureAwait(false);
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn(
                $"[{nameof(UnwrapPacketMiddleware)}] No transformer found for {current.GetType().Name}. " +
                $"OpCode={context.Attributes.OpCode}, From={context.Connection.RemoteEndPoint}, Error={ex}");

            _ = await context.Connection.Tcp.SendAsync("Packet transform failed.")
                                            .ConfigureAwait(false);
        }

        await next();
    }
}