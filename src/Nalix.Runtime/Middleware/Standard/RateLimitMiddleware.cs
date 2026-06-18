// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Framework.Injection;
using Nalix.Runtime.Internal.RateLimiting;
using Nalix.Runtime.Throttling;

namespace Nalix.Runtime.Middleware.Standard;

/// <summary>
/// Middleware that enforces rate limiting for inbound packets based on the remote IP address.
/// </summary>
[MiddlewareOrder(50)] // Execute after security checks
[MiddlewareStage(MiddlewareStage.Inbound)]
public class RateLimitMiddleware : IPacketMiddleware<IPacket>
{
    private readonly PolicyRateLimiter _policy;
    private readonly TokenBucketLimiter _global;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class
    /// using rate limit options retrieved from the global configuration store.
    /// </summary>
    public RateLimitMiddleware()
    {
        _policy = InstanceManager.Instance.GetOrCreateInstance<PolicyRateLimiter>();
        _global = InstanceManager.Instance.GetOrCreateInstance<TokenBucketLimiter>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class
    /// with explicit dependencies.
    /// </summary>
    /// <param name="policyRate">The policy rate limiter.</param>
    /// <param name="tokenBucket">The token bucket limiter.</param>
    public RateLimitMiddleware(PolicyRateLimiter policyRate, TokenBucketLimiter tokenBucket)
    {
        _policy = policyRate ?? throw new ArgumentNullException(nameof(policyRate));
        _global = tokenBucket ?? throw new ArgumentNullException(nameof(tokenBucket));
    }

    /// <summary>
    /// Invokes the rate limiting middleware for inbound packets. Checks if the packet exceeds the configured rate limit for the remote IP address.
    /// If the rate limit is exceeded, the packet is not processed further.
    /// No async state machine is allocated when the request is allowed and the chain completes synchronously.
    /// </summary>
    /// <param name="context">The packet context containing the packet, connection, and metadata.</param>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        TokenBucketLimiter.RateLimitDecision decision;
        PacketRateLimitAttribute? rl = context.Attributes.RateLimit;

        try
        {
            if (rl is not null)
            {
                // Attribute-driven policy: use centralized policy-based limiter
                decision = _policy.Evaluate(context);
            }
            else
            {
                // No attribute: fallback to a global per-endpoint limiter
                decision = _global.Evaluate(context.Connection.NetworkEndpoint);
            }
        }
        catch (ObjectDisposedException)
        {
            // If the limiter has been disposed (e.g., during shutdown), deny the packet (fail-closed)
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Warning,
                    new DiagnosticLog(
                        "RT.RateLimitMiddleware:InvokeAsync",
                        "rate-limiter-disposed request-denied"));
            }
            return default;
        }

        if (!decision.Allowed)
        {
            // Cold path: rate-limited — requires async I/O to send directive.
            return SEND_RATELIMITED_ASYNC(context, decision);
        }

        // Hot path: allowed — forward directly without async state machine.
        ValueTask pending = next(context.CancellationToken);
        if (pending.IsCompletedSuccessfully)
        {
#pragma warning disable CA1849 // Completed-success fast path.
            pending.GetAwaiter().GetResult();
#pragma warning restore CA1849
            return default;
        }
        return AWAIT_NEXT_ASYNC(pending);

        static async ValueTask AWAIT_NEXT_ASYNC(ValueTask operation) => await operation.ConfigureAwait(false);

        static async ValueTask SEND_RATELIMITED_ASYNC(IPacketContext<IPacket> ctx, TokenBucketLimiter.RateLimitDecision dec)
        {
            if (!DirectiveGuard.TryAcquire(ctx.Connection,
                state => state.InboundDirectiveRateLimitedLastSentAtMs,
                (state, val) => state.InboundDirectiveRateLimitedLastSentAtMs = val))
            {
                return;
            }

            // Unified response format: FAIL + RETRY (consistent with RateLimitMiddleware)
            using PacketScope<Directive> lease = PacketFactory<Directive>.Acquire();
            Directive directive = lease.Value;

            directive.Initialize(
                ControlType.FAIL, ProtocolReason.RATE_LIMITED, ProtocolAdvice.RETRY,
                sequenceId: ctx.Packet.Header.SequenceId,
                controlFlags: ControlFlags.IS_TRANSIENT,
                arg0: ctx.Packet.Header.OpCode,
                arg1: (uint)dec.RetryAfterMs,
                arg2: dec.Credit);

            await ctx.Sender.SendAsync(directive, ctx.CancellationToken).ConfigureAwait(false);
        }
    }
}
