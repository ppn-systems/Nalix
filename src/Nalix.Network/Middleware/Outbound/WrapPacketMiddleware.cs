// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Outbound;

/// <summary>
/// Middleware that wraps a packet with compression and encryption as needed before dispatch.
/// </summary>
public class WrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        IPacket current = context.Packet;

        System.Boolean needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;
        System.Boolean needCompress = ShouldCompress(context);

        if (!needEncrypt && !needCompress)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            IPacketCatalog? catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>();
            if (catalog is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Fatal($"[{nameof(WrapPacketMiddleware)}] missing-catalog");

                await context.Connection.SendAsync(
                      controlType: ControlType.FAIL,
                      reason: ProtocolCode.INTERNAL_ERROR,
                      action: ProtocolAction.NONE,
                      sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                      flags: ControlFlags.NONE,
                      arg0: context.Attributes.OpCode.OpCode,
                      arg1: (System.UInt32)current.Flags,
                      arg2: 0).ConfigureAwait(false);

                return;
            }

            if (!catalog.TryGetTransformer(current.GetType(), out PacketTransformer t))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(WrapPacketMiddleware)}] no-transformer type={current.GetType().Name}");

                await context.Connection.SendAsync(
                      controlType: ControlType.FAIL,
                      reason: ProtocolCode.UNSUPPORTED_PACKET,
                      action: ProtocolAction.NONE,
                      sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
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
                                            .Error($"[{nameof(WrapPacketMiddleware)}] no-compress type={current.GetType().Name}");

                    await context.Connection.SendAsync(
                          controlType: ControlType.FAIL,
                          reason: ProtocolCode.COMPRESSION_UNSUPPORTED,
                          action: ProtocolAction.NONE,
                          sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
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
                                            .Error($"[{nameof(WrapPacketMiddleware)}] no-encrypt type={current.GetType().Name}");

                    await context.Connection.SendAsync(
                          controlType: ControlType.FAIL,
                          reason: ProtocolCode.CRYPTO_UNSUPPORTED,
                          action: ProtocolAction.NONE,
                          sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                          flags: ControlFlags.NONE,
                          arg0: context.Attributes.OpCode.OpCode,
                          arg1: (System.UInt32)current.Flags,
                          arg2: 0).ConfigureAwait(false);

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
            await context.Connection.SendAsync(
                  controlType: ControlType.FAIL,
                  reason: ProtocolCode.TRANSFORM_FAILED,
                  action: ProtocolAction.RETRY,
                  sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                  flags: ControlFlags.IS_TRANSIENT,
                  arg0: context.Attributes.OpCode.OpCode,
                  arg1: (System.UInt32)current.Flags,
                  arg2: 0).ConfigureAwait(false);
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }

    private static System.Boolean ShouldCompress(in PacketContext<IPacket> context)
    {
        return context.Packet.Transport == ProtocolType.TCP
            ? context.Packet.Length - PacketConstants.CompressionThreshold > PacketConstants.CompressionThreshold
            : context.Packet.Transport == ProtocolType.UDP &&
              context.Packet.Length - PacketConstants.CompressionThreshold is > 600 and < 1200;
    }
}
