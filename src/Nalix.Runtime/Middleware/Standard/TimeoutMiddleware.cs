// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.Pooling;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Runtime.Middleware.Internal;

namespace Nalix.Runtime.Middleware.Standard;

/// <summary>
/// Middleware that enforces a timeout for packet processing. If the next middleware or handler does not complete within the specified timeout,
/// a timeout response is sent to the client.
/// </summary>
[MiddlewareOrder(75)] // Execute late in inbound, wrap around handler
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class TimeoutMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        int timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;
        if (timeout <= 0)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        using CancellationTokenSource timeoutCts = context.CancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken)
            : new CancellationTokenSource();

        timeoutCts.CancelAfter(timeout);
        CancellationToken tokenToUse = timeoutCts.Token;
        await ExecuteHandlerAsync(timeout, context, next, tokenToUse).ConfigureAwait(false);
    }

    private static async ValueTask ExecuteHandlerAsync(
        int timeout,
        IPacketContext<IPacket> context,
        Func<CancellationToken, ValueTask> next,
        CancellationToken token)
    {
        try
        {
            await next(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            if (!DirectiveGuard.TryAcquire(
                context.Connection,
                ConnectionAttributes.InboundDirectiveTimeoutLastSentAtMs))
            {
                return;
            }

            using PacketLease<Directive> lease = PacketPool<Directive>.Rent();
            Directive directive = lease.Value;

            directive.Initialize(
                ControlType.TIMEOUT, ProtocolReason.TIMEOUT, ProtocolAdvice.RETRY,
                sequenceId: context.Packet.SequenceId,
                controlFlags: ControlFlags.IS_TRANSIENT,
                arg0: (uint)(timeout / 100));

            await context.Sender.SendAsync(directive, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
