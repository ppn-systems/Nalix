// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Network.Pipeline.Inbound;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
[MiddlewareOrder(-50)] // Execute early for security
[MiddlewareStage(MiddlewareStage.Inbound)]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private readonly ILogger? _logger;

    /// <inheritdoc/>
    public PermissionMiddleware() => _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <inheritdoc/>
    public PermissionMiddleware(ILogger logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Invokes the concurrency middleware, enforcing concurrency limits on incoming packets.
    /// </summary>
    /// <param name="context">The packet context containing the packet and connection information.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Attributes.Permission is null ||
            context.Attributes.Permission.Level <= context.Connection.Level)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        _logger?.Trace(
            $"[NW.{nameof(PermissionMiddleware)}] deny op=0x{context.Attributes.PacketOpcode.OpCode:X4} " +
            $"need={context.Attributes.Permission.Level} have={context.Connection.Level}");

        Directive directive = s_pool.Get<Directive>();

        try
        {
            directive.Initialize(
                ControlType.TIMEOUT,
                ProtocolReason.UNAUTHENTICATED, ProtocolAdvice.NONE,
                sequenceId: context.Packet.SequenceId,
                flags: ControlFlags.IS_TRANSIENT,
                arg0: (byte)context.Attributes.Permission.Level,
                arg1: (byte)context.Connection.Level,
                arg2: context.Attributes.PacketOpcode.OpCode);

            using BufferLease lease = BufferLease.Rent(directive.Length + 32);

            int length = directive.Serialize(lease.SpanFull);
            lease.CommitLength(length);
            await context.Connection.TCP.SendAsync(lease.Memory).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.Error($"[NW.{nameof(PermissionMiddleware)}] send-error-failed", ex);
        }
        finally
        {
            s_pool.Return(directive);
        }
    }
}
