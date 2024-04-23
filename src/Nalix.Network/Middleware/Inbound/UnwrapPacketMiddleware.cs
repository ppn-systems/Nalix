// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Models;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that unwraps (decrypts and/or decompresses) packets before further processing.
/// </summary>
[PacketMiddleware(MiddlewareStage.Inbound, order: 3, name: "Unwrap")]
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

        System.UInt32 sequenceId = 0;
        if (context.Packet is IPacketSequenced s)
        {
            sequenceId = s.SequenceId;
        }

        try
        {
            IPacketCatalog? catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>();
            if (catalog is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Fatal($"[{nameof(UnwrapPacketMiddleware)}] missing-catalog");

                await context.Connection.SendAsync(
                    ControlType.FAIL,
                    ProtocolCode.INTERNAL_ERROR,
                    ProtocolAction.NONE,
                    sequenceId: sequenceId,
                    flags: ControlFlags.NONE,
                    arg0: context.Attributes.OpCode.OpCode,
                    arg1: (System.Byte)current.Flags,
                    arg2: 0).ConfigureAwait(false);

                return;
            }

            if (!catalog.TryGetTransformer(current.GetType(), out PacketTransformer t))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(UnwrapPacketMiddleware)}] no-transformer type={current.GetType().Name}");

                await context.Connection.SendAsync(
                    ControlType.FAIL,
                    ProtocolCode.UNSUPPORTED_PACKET,
                    ProtocolAction.NONE,
                    sequenceId: sequenceId,
                    flags: ControlFlags.NONE,
                    arg0: context.Attributes.OpCode.OpCode,
                    arg1: (System.Byte)current.Flags,
                    arg2: 0).ConfigureAwait(false);

                return;
            }

            if (needDecrypt)
            {
                if (!t.HasDecrypt)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn(
                        $"[{nameof(UnwrapPacketMiddleware)}] no-decrypt type={current.GetType().Name}");

                    await context.Connection.SendAsync(
                        ControlType.FAIL,
                        ProtocolCode.CRYPTO_UNSUPPORTED,
                        ProtocolAction.NONE,
                        sequenceId: sequenceId,
                        flags: ControlFlags.NONE,
                        arg0: context.Attributes.OpCode.OpCode,
                        arg1: (System.Byte)current.Flags,
                        arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Decrypt(current, context.Connection.EncryptionKey, context.Connection.Encryption);
            }

            if (needDecompress)
            {
                if (!t.HasDecompress)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn(
                        $"[{nameof(UnwrapPacketMiddleware)}] no-decompress type={current.GetType().Name}");

                    await context.Connection.SendAsync(
                        ControlType.FAIL,
                        ProtocolCode.COMPRESSION_UNSUPPORTED,
                        ProtocolAction.NONE,
                        sequenceId: sequenceId,
                        flags: ControlFlags.NONE,
                        arg0: context.Attributes.OpCode.OpCode,
                        arg1: (System.UInt32)current.Flags,
                        arg2: 0).ConfigureAwait(false);

                    return;
                }
                current = t.Decompress(current);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.AssignPacket(current);
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(UnwrapPacketMiddleware)}] transform-failed type={current.GetType().Name}", ex);

            await context.Connection.SendAsync(
                ControlType.FAIL,
                ProtocolCode.TRANSFORM_FAILED,
                ProtocolAction.RETRY,
                sequenceId: sequenceId,
                flags: ControlFlags.IS_TRANSIENT,
                arg0: context.Attributes.OpCode.OpCode,
                arg1: (System.Byte)current.Flags,
                arg2: 0).ConfigureAwait(false);
        }

        await next();
    }
}