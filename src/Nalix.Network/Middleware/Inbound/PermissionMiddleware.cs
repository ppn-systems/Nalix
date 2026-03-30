// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Routing;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that checks the permission level of the current connection
/// before allowing the packet to proceed to the next handler.
/// </summary>
[MiddlewareOrder(-50)] // Execute early for security
[MiddlewareStage(MiddlewareStage.Inbound)]
public class PermissionMiddleware : IPacketMiddleware<IPacket>
{
    private readonly ILogger? _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Invokes the concurrency middleware, enforcing concurrency limits on incoming packets.
    /// </summary>
    /// <param name="context">The packet context containing the packet and connection information.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(PacketContext<IPacket> context, Func<CancellationToken, Task> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Attributes.Permission is null ||
            context.Attributes.Permission.Level <= context.Connection.Level)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (_logger?.IsEnabled(LogLevel.Trace) == true)
        {
            _logger.LogTrace(
                "[NW.PermissionMiddleware] deny op=0x{OpCode:X4} need={NeedLevel} have={HaveLevel}",
                context.Attributes.PacketOpcode.OpCode,
                context.Attributes.Permission.Level,
                context.Connection.Level
            );
        }

        try
        {
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.UNAUTHENTICATED,
                action: ProtocolAdvice.NONE,
                options: new ControlDirectiveOptions(
                    SequenceId: context.Packet.SequenceId,
                    Arg0: (byte)context.Attributes.Permission.Level,
                    Arg1: (byte)context.Connection.Level,
                    Arg2: context.Attributes.PacketOpcode.OpCode)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(
                    ex,
                    "[NW.PermissionMiddleware] send-error-failed"
                );
            }
        }
    }
}
