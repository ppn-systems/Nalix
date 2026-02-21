// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Diagnostics;
using Nalix.Common.Enums;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Outbound;

/// <summary>
/// Middleware that wraps a packet with compression and encryption as needed before dispatch.
/// </summary>
[MiddlewareOrder(100)] // Execute last in outbound
[MiddlewareStage(MiddlewareStage.Outbound)]
public class WrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        IPacket current = context.Packet;

        System.Boolean needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;
        System.Boolean needCompress = SHOULD_COMPRESS(context);

        if (!needEncrypt && !needCompress)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            IPacketCatalog catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>();
            if (catalog is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Fatal($"[NW.{nameof(WrapPacketMiddleware)}] missing-catalog");

                System.UInt32 sequenceId1 = context.Packet is IPacketSequenced sequenced1
                    ? sequenced1.SequenceId
                    : 0;

                await context.Connection.SendAsync(
                      controlType: ControlType.FAIL,
                      reason: ProtocolReason.INTERNAL_ERROR,
                      action: ProtocolAdvice.NONE,
                      sequenceId: sequenceId1,
                      flags: ControlFlags.NONE,
                      arg0: context.Attributes.OpCode.OpCode,
                      arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                return;
            }

            if (!catalog.TryGetTransformer(current.GetType(), out PacketTransformer t))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[NW.{nameof(WrapPacketMiddleware)}] no-transformer type={current.GetType().Name}");

                System.UInt32 sequenceId2 = context.Packet is IPacketSequenced sequenced2 ? sequenced2.SequenceId : 0;

                await context.Connection.SendAsync(
                      controlType: ControlType.FAIL,
                      reason: ProtocolReason.UNSUPPORTED_PACKET,
                      action: ProtocolAdvice.NONE,
                      sequenceId: sequenceId2,
                      flags: ControlFlags.NONE,
                      arg0: context.Attributes.OpCode.OpCode,
                      arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                return;
            }

            if (needCompress)
            {
                if (!t.HasCompress)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(WrapPacketMiddleware)}] no-compress type={current.GetType().Name}");

                    System.UInt32 sequenceId3 = context.Packet is IPacketSequenced sequenced3
                        ? sequenced3.SequenceId
                        : 0;

                    await context.Connection.SendAsync(
                          controlType: ControlType.FAIL,
                          reason: ProtocolReason.COMPRESSION_UNSUPPORTED,
                          action: ProtocolAdvice.NONE,
                          sequenceId: sequenceId3,
                          flags: ControlFlags.NONE,
                          arg0: context.Attributes.OpCode.OpCode,
                          arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Compress(current);
            }

            if (needEncrypt)
            {
                if (!t.HasEncrypt)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(WrapPacketMiddleware)}] no-encrypt type={current.GetType().Name}");

                    System.UInt32 sequenceId4 = context.Packet is IPacketSequenced sequenced4
                        ? sequenced4.SequenceId
                        : 0;

                    await context.Connection.SendAsync(
                          controlType: ControlType.FAIL,
                          reason: ProtocolReason.CRYPTO_UNSUPPORTED,
                          action: ProtocolAdvice.NONE,
                          sequenceId: sequenceId4,
                          flags: ControlFlags.NONE,
                          arg0: context.Attributes.OpCode.OpCode,
                          arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Encrypt(current, context.Connection.Secret, context.Connection.Algorithm);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.AssignPacket(current);
            }
        }
        catch (System.Exception)
        {
            System.UInt32 sequenceId5 = context.Packet is IPacketSequenced sequenced5
                ? sequenced5.SequenceId
                : 0;

            await context.Connection.SendAsync(
                  controlType: ControlType.FAIL,
                  reason: ProtocolReason.TRANSFORM_FAILED,
                  action: ProtocolAdvice.RETRY,
                  sequenceId: sequenceId5,
                  flags: ControlFlags.IS_TRANSIENT,
                  arg0: context.Attributes.OpCode.OpCode,
                  arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }

    private static System.Boolean SHOULD_COMPRESS(in PacketContext<IPacket> context)
    {
        return context.Packet.Protocol == ProtocolType.TCP
            ? context.Packet.Length - PacketConstants.CompressionThreshold > PacketConstants.CompressionThreshold
            : context.Packet.Protocol == ProtocolType.UDP &&
              context.Packet.Length - PacketConstants.CompressionThreshold is > 600 and < 1200;
    }
}
