// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;

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
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        System.Int32 timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;
        if (timeout <= 0)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        using System.Threading.CancellationTokenSource timeoutCts = new(timeout);
        using System.Threading.CancellationTokenSource linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);

        try
        {
            await next(linkedCts.Token).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) when (timeoutCts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            System.UInt32 sequenceId = context.Packet is IPacketSequenced sequenced ? sequenced.SequenceId : 0;

            try
            {
                await context.Connection.SendAsync(
                    ControlType.TIMEOUT,
                    ProtocolReason.TIMEOUT,
                    ProtocolAdvice.RETRY,
                    sequenceId: sequenceId,
                    flags: ControlFlags.IS_TRANSIENT,
                    arg0: (System.UInt32)(timeout / 100),
                    arg1: 0, arg2: 0).ConfigureAwait(false);
            }
            catch
            {
                // Ignore send failures
            }

            throw;
        }
    }
}
