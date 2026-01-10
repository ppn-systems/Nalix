using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Protocols;
using Nalix.Network.Abstractions;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch;
using Nalix.Network.Throttling;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces rate limiting for inbound packets based on the remote IP address.
/// </summary>
public class RateLimitMiddleware : IPacketMiddleware<IPacket>
{
    /// <summary>
    /// Invokes the rate limiting middleware for inbound packets. Checks if the packet exceeds the configured rate limit for the remote IP address.
    /// If the rate limit is exceeded, the packet is not processed further.
    /// </summary>
    /// <param name="context">The packet context containing the packet, connection, and metadata.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        PacketRateLimitAttribute rl = context.Attributes.RateLimit;
        if (rl is null)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        System.String ip = context.Connection.EndPoint.ToString();

        if (System.String.IsNullOrEmpty(ip))
        {
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.RATE_LIMITED,
                action: ProtocolAdvice.RETRY,
                sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                flags: ControlFlags.IS_TRANSIENT).ConfigureAwait(false);

            return;
        }

        TokenBucketLimiter.RateLimitDecision d = PolicyRateLimiter.Check(context.Packet.OpCode, context);

        if (!d.Allowed)
        {
            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.RATE_LIMITED,
                action: ProtocolAdvice.RETRY,
                sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                flags: ControlFlags.IS_TRANSIENT,
                arg0: context.Attributes.OpCode.OpCode,
                arg1: (System.UInt32)d.RetryAfterMs, arg2: d.Credit).ConfigureAwait(false);

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}

