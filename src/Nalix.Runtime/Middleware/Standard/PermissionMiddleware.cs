// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Runtime.Internal.RateLimiting;
using Nalix.Runtime.Pooling;

namespace Nalix.Runtime.Middleware.Standard;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
[MiddlewareOrder(-50)] // Execute early for security
[MiddlewareStage(MiddlewareStage.Inbound)]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
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

        // SEC-37: Fail-closed by default. If no permission attribute is defined on the handler,
        // deny the request to prevent accidental privilege escalation from missing annotations.
        if (context.Attributes.Permission is not null &&
            context.Attributes.Permission.Level <= context.Connection.Level)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                $"[NW.{nameof(PermissionMiddleware)}] deny op=0x{context.Attributes.PacketOpcode.OpCode:X4} " +
                $"need={context.Attributes.Permission?.Level.ToString() ?? "N/A (no attribute)"} have={context.Connection.Level}");
        }

        if (!DirectiveGuard.TryAcquire(
            context.Connection,
            ConnectionAttributes.InboundDirectiveUnauthorizedLastSentAtMs))
        {
            return;
        }

        using PacketScope<Directive> lease = PacketFactory<Directive>.Acquire();
        Directive directive = lease.Value;

        try
        {
            directive.Initialize(
                ControlType.FAIL,
                ProtocolReason.UNAUTHORIZED, ProtocolAdvice.NONE,
                sequenceId: context.Packet.Header.SequenceId,
                controlFlags: ControlFlags.NONE,
                arg0: 0,
                arg1: 0,
                arg2: context.Attributes.PacketOpcode.OpCode);

            await context.Sender.SendAsync(directive).ConfigureAwait(false);
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            context.Connection.ThrottledError(_logger, "middleware.permission.send_error", $"[NW.{nameof(PermissionMiddleware)}] send-error-failed", ex);
        }
    }
}
