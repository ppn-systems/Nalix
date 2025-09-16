using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;
using Nalix.Network.Internal.Net;
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
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next,
        System.Threading.CancellationToken ct)
    {
        var rl = context.Attributes.RateLimit;
        if (rl is null)
        {
            await next(ct).ConfigureAwait(false);
            return;
        }

        if (context.Connection.RemoteEndPoint is System.Net.IPEndPoint ipEndPoint)
        {
            TokenBucketLimiter.LimitDecision d = PolicyRateLimiter.Check(
                context.Packet.OpCode, rl,
                IpAddressKey.FromIpAddress(ipEndPoint.Address).ToString());

            if (!d.Allowed)
            {
                await context.Connection.SendAsync(
                    controlType: ControlType.FAIL,
                    reason: ProtocolCode.RATE_LIMITED,
                    action: ProtocolAction.RETRY,
                    sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                    flags: ControlFlags.IS_TRANSIENT,
                    arg0: context.Attributes.OpCode.OpCode,
                    arg1: (System.UInt32)d.RetryAfterMs, arg2: d.Credit
                ).ConfigureAwait(false);
                return;
            }
        }

        await next(ct).ConfigureAwait(false);
    }
}

