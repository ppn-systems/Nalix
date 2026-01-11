// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that unwraps (decrypts and/or decompresses) packets before further processing.
/// </summary>
public class UnwrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        IPacket current = context.Packet;

        System.Boolean needDecrypt = current.Flags.HasFlag(PacketFlags.ENCRYPTED);
        System.Boolean needDecompress = current.Flags.HasFlag(PacketFlags.COMPRESSED);

        if (!needDecrypt && !needDecompress)
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
                                        .Fatal($"[NW.{nameof(UnwrapPacketMiddleware)}] missing-catalog");

                System.UInt32 sequenceId1 = context.Packet is IPacketSequenced sequenced1
                    ? sequenced1.SequenceId
                    : 0;

                await context.Connection.SendAsync(
                    ControlType.FAIL,
                    ProtocolReason.INTERNAL_ERROR,
                    ProtocolAdvice.NONE,
                    sequenceId: sequenceId1,
                    flags: ControlFlags.NONE,
                    arg0: context.Attributes.OpCode.OpCode,
                    arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                return;
            }

            if (!catalog.TryGetTransformer(current.GetType(), out PacketTransformer t))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[NW.{nameof(UnwrapPacketMiddleware)}] no-transformer type={current.GetType().Name}");

                System.UInt32 sequenceId2 = context.Packet is IPacketSequenced sequenced2
                    ? sequenced2.SequenceId
                    : 0;

                await context.Connection.SendAsync(
                    ControlType.FAIL,
                    ProtocolReason.UNSUPPORTED_PACKET,
                    ProtocolAdvice.NONE,
                    sequenceId: sequenceId2,
                    flags: ControlFlags.NONE,
                    arg0: context.Attributes.OpCode.OpCode,
                    arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                return;
            }

            if (needDecrypt)
            {
                if (!t.HasDecrypt)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn(
                        $"[NW.{nameof(UnwrapPacketMiddleware)}] no-decrypt type={current.GetType().Name}");

                    System.UInt32 sequenceId3 = context.Packet is IPacketSequenced sequenced3
                        ? sequenced3.SequenceId
                        : 0;

                    await context.Connection.SendAsync(
                        ControlType.FAIL,
                        ProtocolReason.CRYPTO_UNSUPPORTED,
                        ProtocolAdvice.NONE,
                        sequenceId: sequenceId3,
                        flags: ControlFlags.NONE,
                        arg0: context.Attributes.OpCode.OpCode,
                        arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Decrypt(current, context.Connection.Secret);
            }

            if (needDecompress)
            {
                if (!t.HasDecompress)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn(
                        $"[NW.{nameof(UnwrapPacketMiddleware)}] no-decompress type={current.GetType().Name}");

                    System.UInt32 sequenceId4 = context.Packet is IPacketSequenced sequenced4
                        ? sequenced4.SequenceId
                        : 0;

                    await context.Connection.SendAsync(
                        ControlType.FAIL,
                        ProtocolReason.COMPRESSION_UNSUPPORTED,
                        ProtocolAdvice.NONE,
                        sequenceId: sequenceId4,
                        flags: ControlFlags.NONE,
                        arg0: context.Attributes.OpCode.OpCode,
                        arg1: (System.UInt32)current.Flags, arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Decompress(current);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[NW.{nameof(UnwrapPacketMiddleware)}] packet-replaced " +
                                               $"type={current.GetType().Name} op=0x{context.Attributes.OpCode.OpCode:X}");
                context.AssignPacket(current);
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[NW.{nameof(UnwrapPacketMiddleware)}] packet-in-place " +
                                               $"type={current.GetType().Name} op=0x{context.Attributes.OpCode.OpCode:X}");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(UnwrapPacketMiddleware)}] transform-failed type={current.GetType().Name}", ex);

            System.UInt32 sequenceId5 = context.Packet is IPacketSequenced sequenced5
                ? sequenced5.SequenceId
                : 0;

            await context.Connection.SendAsync(
                ControlType.FAIL,
                ProtocolReason.TRANSFORM_FAILED,
                ProtocolAdvice.RETRY,
                sequenceId: sequenceId5,
                flags: ControlFlags.IS_TRANSIENT,
                arg0: context.Attributes.OpCode.OpCode,
                arg1: (System.Byte)current.Flags, arg2: 0).ConfigureAwait(false);

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}