// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection.Protocols;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Models;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;
using Nalix.Shared.Injection;
using Nalix.Shared.Messaging.Catalog;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that wraps a packet with compression and encryption as needed before dispatch.
/// </summary>
[PacketMiddleware(MiddlewareStage.Inbound, order: 2, name: "Wrap")]
public class WrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        IPacket current = context.Packet;

        System.Boolean needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;
        System.Boolean needCompress = ShouldCompress(context);

        if (!needEncrypt && !needCompress)
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
                                        .Error($"[{nameof(WrapPacketMiddleware)}] Missing PacketCatalog.");

                await context.Connection.SendAsync(
                      controlType: ControlType.FAIL,
                      reason: ReasonCode.CANCELLED,
                      action: SuggestedAction.NONE,
                      flags: ControlFlags.NONE,
                      arg0: context.Attributes.OpCode.OpCode,
                      arg1: (System.UInt32)current.Flags,
                      arg2: 0).ConfigureAwait(false);

                return;
            }

            if (!catalog.TryGetTransformer(current.GetType(), out PacketTransformer t))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(WrapPacketMiddleware)}] " +
                                               $"No transformer found for packet type {current.GetType().Name}.");

                await context.Connection.SendAsync(
                      controlType: ControlType.FAIL,
                      reason: ReasonCode.UNSUPPORTED_PACKET,
                      action: SuggestedAction.NONE,
                      flags: ControlFlags.NONE,
                      arg0: context.Attributes.OpCode.OpCode,
                      arg1: (System.UInt32)current.Flags,
                      arg2: 0).ConfigureAwait(false);

                return;
            }

            if (needCompress)
            {
                if (!t.HasCompress)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(WrapPacketMiddleware)}] " +
                                                   $"No compression delegate found for packet type {current.GetType().Name}.");

                    await context.Connection.SendAsync(
                          controlType: ControlType.FAIL,
                          reason: ReasonCode.COMPRESSION_UNSUPPORTED,
                          action: SuggestedAction.NONE,
                          flags: ControlFlags.NONE,
                          arg0: context.Attributes.OpCode.OpCode,
                          arg1: (System.UInt32)current.Flags,
                          arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Compress(current);
            }

            if (needEncrypt)
            {
                if (!t.HasEncrypt)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(WrapPacketMiddleware)}] " +
                                                   $"No encryption delegate found for packet type {current.GetType().Name}.");

                    await context.Connection.SendAsync(
                          controlType: ControlType.FAIL,
                          reason: ReasonCode.CRYPTO_UNSUPPORTED,
                          action: SuggestedAction.NONE,
                          flags: ControlFlags.NONE,
                          arg0: context.Attributes.OpCode.OpCode,
                          arg1: (System.UInt32)current.Flags,
                          arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Encrypt(current, context.Connection.EncryptionKey, context.Connection.Encryption);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.SetPacket(current);
            }
        }
        catch (System.Exception)
        {
            await context.Connection.SendAsync(
                  controlType: ControlType.FAIL,
                  reason: ReasonCode.TRANSFORM_FAILED,
                  action: SuggestedAction.RETRY,
                  flags: ControlFlags.IS_TRANSIENT,
                  arg0: context.Attributes.OpCode.OpCode,
                  arg1: (System.UInt32)current.Flags,
                  arg2: 0).ConfigureAwait(false);
        }

        await next();
    }

    private static System.Boolean ShouldCompress(in PacketContext<IPacket> context)
    {
        return context.Packet.Transport == TransportProtocol.TCP
            ? context.Packet.Length - PacketConstants.CompressionThreshold > PacketConstants.CompressionThreshold
            : context.Packet.Transport == TransportProtocol.UDP &&
              context.Packet.Length - PacketConstants.CompressionThreshold is > 600 and < 1200;
    }
}
