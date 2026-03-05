// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that unwraps (decrypts and/or decompresses) packets before further processing.
/// </summary>
[MiddlewareOrder(-100)] // Execute first in inbound
[MiddlewareStage(MiddlewareStage.Inbound)]
public class UnwrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    private readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private readonly IPacketCatalog s_catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>();

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

        if (s_catalog is null)
        {
            s_logger?.Fatal($"[NW.{nameof(UnwrapPacketMiddleware)}] missing-catalog");
            await SEND_ERROR_RESPONSE(context, ProtocolReason.INTERNAL_ERROR, ControlFlags.NONE).ConfigureAwait(false);
            return;
        }

        if (!s_catalog.TryGetTransformer(current.GetType(), out PacketTransformer transformer))
        {
            s_logger?.Error($"[NW.{nameof(UnwrapPacketMiddleware)}] no-transformer type={current.GetType().Name}");
            await SEND_ERROR_RESPONSE(context, ProtocolReason.UNSUPPORTED_PACKET, ControlFlags.NONE).ConfigureAwait(false);
            return;
        }

        try
        {
            if (needDecrypt)
            {
                if (!transformer.HasDecrypt)
                {
                    s_logger?.Warn($"[NW.{nameof(UnwrapPacketMiddleware)}] no-decrypt type={current.GetType().Name}");
                    await SEND_ERROR_RESPONSE(context, ProtocolReason.CRYPTO_UNSUPPORTED, ControlFlags.NONE).ConfigureAwait(false);
                    return;
                }

                current = transformer.Decrypt(current, context.Connection.Secret);
            }

            if (needDecompress)
            {
                if (!transformer.HasDecompress)
                {
                    s_logger?.Warn($"[NW.{nameof(UnwrapPacketMiddleware)}] no-decompress type={current.GetType().Name}");
                    await SEND_ERROR_RESPONSE(context, ProtocolReason.COMPRESSION_UNSUPPORTED, ControlFlags.NONE).ConfigureAwait(false);
                    return;
                }

                current = transformer.Decompress(current);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                s_logger?.Trace($"[NW.{nameof(UnwrapPacketMiddleware)}] packet-replaced type={current.GetType().Name} op=0x{context.Attributes.OpCode.OpCode:X4}");
                context.AssignPacket(current);
            }
        }
        catch (System.IO.InvalidDataException ex)
        {
            s_logger?.Warn($"[NW.{nameof(UnwrapPacketMiddleware)}] decompress-failed type={current.GetType().Name}", ex);
            await SEND_ERROR_RESPONSE(context, ProtocolReason.COMPRESSION_FAILED, ControlFlags.NONE).ConfigureAwait(false);
            return;
        }
        catch (System.Exception ex)
        {
            s_logger?.Warn($"[NW.{nameof(UnwrapPacketMiddleware)}] transform-failed type={current.GetType().Name}", ex);
            await SEND_ERROR_RESPONSE(context, ProtocolReason.TRANSFORM_FAILED, ControlFlags.IS_TRANSIENT).ConfigureAwait(false);
            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.Task SEND_ERROR_RESPONSE(PacketContext<IPacket> context, ProtocolReason reason, ControlFlags flags)
    {
        System.UInt32 sequenceId = context.Packet is IPacketSequenced sequenced ? sequenced.SequenceId : 0;

        try
        {
            await context.Connection.SendAsync(
                ControlType.FAIL,
                reason,
                ProtocolAdvice.NONE,
                sequenceId: sequenceId,
                flags: flags,
                arg0: context.Attributes.OpCode.OpCode,
                arg1: (System.UInt32)context.Packet.Flags,
                arg2: 0).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(UnwrapPacketMiddleware)}] send-error-failed reason={reason}", ex);
        }
    }
}
