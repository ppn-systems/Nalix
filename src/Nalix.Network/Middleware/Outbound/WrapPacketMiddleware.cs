// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Middleware.Attributes;
using Nalix.Common.Middleware.Enums;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Core;
using Nalix.Common.Networking.Packets.Transformation;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Outbound;

/// <summary>
/// Middleware that wraps a packet with compression and encryption as needed before dispatch.
/// </summary>
[MiddlewareOrder(100)]
[MiddlewareStage(MiddlewareStage.Outbound)]
public class WrapPacketMiddleware : IPacketMiddleware<IPacket>
{
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly IPacketCatalog s_catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>();

    /// <summary>
    /// Determines whether the specified packet should be compressed before transmission.
    /// </summary>
    /// <param name="packet">
    /// The packet to evaluate for compression eligibility.
    /// </param>
    /// <returns>
    /// <c>true</c> if the packet meets the compression criteria; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Compression decisions are based on the packet protocol and effective payload size.
    /// </para>
    /// <para>
    /// For <see cref="ProtocolType.TCP"/>, packets are compressed when the payload size
    /// exceeds the configured compression threshold.
    /// </para>
    /// <para>
    /// For <see cref="ProtocolType.UDP"/>, compression is applied only when the payload size
    /// falls within a bounded range to avoid fragmentation and excessive overhead.
    /// </para>
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="packet"/> is <c>null</c>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual System.Boolean ShouldCompress(IPacket packet)
    {
        System.Int32 payloadSize = packet.Length - PacketConstants.CompressionThreshold;

        return packet.Protocol == ProtocolType.TCP
            ? payloadSize > PacketConstants.CompressionThreshold
            : packet.Protocol == ProtocolType.UDP && payloadSize is > 600 and < 1200;
    }

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        IPacket current = context.Packet;

        System.Boolean needEncrypt = context.Attributes.Encryption?.IsEncrypted ?? false;
        System.Boolean needCompress = ShouldCompress(current);

        if (!needEncrypt && !needCompress)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (s_catalog is null)
        {
            s_logger?.Fatal($"[NW.{nameof(WrapPacketMiddleware)}] missing-catalog");
            await SEND_ERROR_RESPONSE(context, ProtocolReason.INTERNAL_ERROR, ControlFlags.NONE).ConfigureAwait(false);
            return;
        }

        if (!s_catalog.TryGetTransformer(current.GetType(), out PacketTransformer transformer))
        {
            s_logger?.Error($"[NW.{nameof(WrapPacketMiddleware)}] no-transformer type={current.GetType().Name}");
            await SEND_ERROR_RESPONSE(context, ProtocolReason.UNSUPPORTED_PACKET, ControlFlags.NONE).ConfigureAwait(false);
            return;
        }

        try
        {
            if (needCompress)
            {
                if (!transformer.HasCompress)
                {
                    s_logger?.Error($"[NW.{nameof(WrapPacketMiddleware)}] no-compress type={current.GetType().Name}");
                    await SEND_ERROR_RESPONSE(context, ProtocolReason.COMPRESSION_UNSUPPORTED, ControlFlags.NONE).ConfigureAwait(false);
                    return;
                }

                current = transformer.Compress(current);
            }

            if (needEncrypt)
            {
                if (!transformer.HasEncrypt)
                {
                    s_logger?.Error($"[NW.{nameof(WrapPacketMiddleware)}] no-encrypt type={current.GetType().Name}");
                    await SEND_ERROR_RESPONSE(context, ProtocolReason.CRYPTO_UNSUPPORTED, ControlFlags.NONE).ConfigureAwait(false);
                    return;
                }

                current = transformer.Encrypt(current, context.Connection.Secret, context.Connection.Algorithm);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.AssignPacket(current);
            }
        }
        catch (System.IO.InvalidDataException ex)
        {
            s_logger?.Warn($"[NW.{nameof(WrapPacketMiddleware)}] compress-failed type={current.GetType().Name} ex={ex.Message}");
            await SEND_ERROR_RESPONSE(context, ProtocolReason.COMPRESSION_FAILED, ControlFlags.NONE).ConfigureAwait(false);
            return;
        }
        catch (System.Exception ex)
        {
            s_logger?.Warn($"[NW.{nameof(WrapPacketMiddleware)}] transform-failed type={current.GetType().Name} ex={ex.Message}");
            await SEND_ERROR_RESPONSE(context, ProtocolReason.TRANSFORM_FAILED, ControlFlags.IS_TRANSIENT).ConfigureAwait(false);
            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static async System.Threading.Tasks.Task SEND_ERROR_RESPONSE(PacketContext<IPacket> context, ProtocolReason reason, ControlFlags flags)
    {
        System.UInt32 sequenceId = context.Packet is IPacketSequenced sequenced ? sequenced.SequenceId : 0;

        try
        {
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: reason,
                action: ProtocolAdvice.NONE,
                sequenceId: sequenceId,
                flags: flags,
                arg0: context.Attributes.OpCode.OpCode,
                arg1: (System.UInt32)context.Packet.Flags,
                arg2: 0).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(WrapPacketMiddleware)}] send-error-failed reason={reason}", ex);
        }
    }
}
