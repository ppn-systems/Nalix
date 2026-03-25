// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Network.Connections;
using Nalix.Network.Routing;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces a timeout for packet processing. If the next middleware or handler does not complete within the specified timeout,
/// a timeout response is sent to the client.
/// </summary>
[MiddlewareOrder(75)] // Execute late in inbound, wrap around handler
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class TimeoutMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async Task InvokeAsync(
        PacketContext<IPacket> context,
        Func<CancellationToken, Task> next)
    {
        int timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;
        if (timeout <= 0)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        CancellationToken tokenToUse;
        using CancellationTokenSource timeoutCts = new(timeout);

        if (timeout > 10_000 && context.CancellationToken.CanBeCanceled)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);
            tokenToUse = linkedCts.Token;
            await ExecuteHandlerAsync(timeout, context, next, tokenToUse);
        }
        else
        {
            tokenToUse = timeoutCts.Token;
            await ExecuteHandlerAsync(timeout, context, next, tokenToUse);
        }
    }

    private static async Task ExecuteHandlerAsync(
        int timeout,
        PacketContext<IPacket> context,
        Func<CancellationToken, Task> next,
        CancellationToken token)
    {
        try
        {
            await next(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            uint sequenceId = context.Packet is IPacketSequenced sequenced ? sequenced.SequenceId : 0;

            try
            {
                await context.Connection.SendAsync(
                    ControlType.TIMEOUT,
                    ProtocolReason.TIMEOUT,
                    ProtocolAdvice.RETRY,
                    sequenceId: sequenceId,
                    flags: ControlFlags.IsTransient,
                    arg0: (uint)(timeout / 100),
                    arg1: 0, arg2: 0
                ).ConfigureAwait(false);
            }
            catch
            {
                // Ignore send failures
            }
            throw;
        }
    }
}
