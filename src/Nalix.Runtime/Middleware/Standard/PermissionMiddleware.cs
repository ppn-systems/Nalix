// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Runtime.Internal.Diagnostics;
using Nalix.Runtime.Internal.RateLimiting;

namespace Nalix.Runtime.Middleware.Standard;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
[MiddlewareOrder(-50)] // Execute early for security
[MiddlewareStage(MiddlewareStage.Inbound)]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    private static readonly Internal.Diagnostics.ThrottleKey s_keySendError = new("middleware.permission.send_error");

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
            IsAuthorized(context.Connection.Level, context.Attributes.Permission))
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
        {
            string needLevel = context.Attributes.Permission?.Level.ToString() ?? "N/A (no attribute)";
            DiagnosticsEvents.Write(
                DiagnosticsEvents.Internal.Trace,
                new DiagnosticLog(
                    "RT.PermissionMiddleware:InvokeAsync",
                    $"deny op=0x{context.Attributes.PacketOpcode.OpCode:X4} need={needLevel} have={context.Connection.Level}"));
        }

        context.Connection.IncrementErrorCount();

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
                arg2: context.Packet.Header.OpCode);

            await context.Sender.SendAsync(directive).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            context.Connection.ThrottledDiagnosticError(
                DiagnosticsEvents.Internal.Error,
                s_keySendError,
                "RT.PermissionMiddleware:InvokeAsync",
                "send-error-failed",
                ex);
        }
    }

    private static bool IsAuthorized(PermissionLevel actorLevel, PacketPermissionAttribute policy)
    {
        return policy.Evaluation switch
        {
            PermissionEvaluation.StrictMatch => actorLevel == policy.Level,
            PermissionEvaluation.MinimumLevel => actorLevel >= policy.Level,
            _ => false
        };
    }
}
