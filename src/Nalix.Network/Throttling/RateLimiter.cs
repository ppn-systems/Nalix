// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Attributes;
using Nalix.Network.Configurations;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Throttling;

/// <summary>
/// Centralized rate limiter for packet handlers.
/// Reuses a single <see cref="TokenBucketLimiter"/> per unique policy (RPS + Burst),
/// and uses composite endpoint keys "op:{opcode}|ep:{endpoint}" to isolate callers.
/// This avoids creating one limiter per endpoint, drastically reducing background tasks.
/// </summary>
public static class RateLimiter
{
    /// <summary>
    /// Try consuming a token for a given opcode and endpoint, under the supplied attribute policy.
    /// </summary>
    /// <param name="opCode">Handler opcode.</param>
    /// <param name="attr">Rate-limit attribute from handler metadata.</param>
    /// <param name="ip">
    /// Optional endpoint dimension (e.g., connection ID, remote IP, tenant ID).
    /// If null, the rate limit is applied globally per opcode.
    /// </param>
    /// <returns>Decision containing Allowed, RetryAfterMs, Credit, and Reason.</returns>
    public static TokenBucketLimiter.LimitDecision Check(
        System.UInt16 opCode,
        PacketRateLimitAttribute attr,
        System.String ip)
    {
        System.ArgumentNullException.ThrowIfNull(attr);
        if (attr.RequestsPerSecond <= 0)
        {
            // disabled
            return Allowed();
        }

        if (attr.Burst <= 0)
        {
            // misconfig: deny all
            return DeniedHard();
        }

        // Fetch or create a shared limiter for this policy.
        TokenBucketLimiter limiter = s_limiters.GetOrAdd(
            new Policy(attr.RequestsPerSecond, attr.Burst),
            static key =>
            {
                TokenBucketOptions opt = new()
                {
                    CapacityTokens = key.Burst,
                    RefillTokensPerSecond = key.RequestsPerSecond,
                    TokenScale = s_defaults.TokenScale,                   // micro-tokens for precision
                    ShardCount = s_defaults.ShardCount,                   // power-of-two
                    HardLockoutSeconds = s_defaults.HardLockoutSeconds,   // per-handler layer: soft throttle only
                    StaleEntrySeconds = s_defaults.StaleEntrySeconds,
                    CleanupIntervalSeconds = s_defaults.CleanupIntervalSeconds
                };
                return new TokenBucketLimiter(opt);
            });

        // Composite key separates traffic per opcode and (optional) endpoint.
        // A single limiter can track all these keys internally.
        System.String key = $"op:{opCode}|ep:{ip}";

        return limiter.Check(key);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static TokenBucketLimiter.LimitDecision Allowed() => new()
        {
            Allowed = true,
            RetryAfterMs = 0,
            Credit = System.UInt16.MaxValue,
            Reason = TokenBucketLimiter.RateLimitReason.None
        };

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static TokenBucketLimiter.LimitDecision DeniedHard() => new()
        {
            Allowed = false,
            RetryAfterMs = System.Int32.MaxValue,
            Credit = 0,
            Reason = TokenBucketLimiter.RateLimitReason.HardLockout
        };
    }

    /// <summary>
    /// Uniquely identifies a rate-limit policy.
    /// Two handlers with the same (RequestsPerSecond, Burst) share the same limiter.
    /// </summary>
    private readonly record struct Policy(System.Int32 RequestsPerSecond, System.Int32 Burst);

    /// <summary>
    /// Cached default options from configuration.
    /// </summary>
    private static readonly TokenBucketOptions s_defaults = ConfigurationManager.Instance.Get<TokenBucketOptions>();

    /// <summary>
    /// One TokenBucketLimiter per policy key. Each limiter can track millions of endpoints internally.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, TokenBucketLimiter> s_limiters = new();
}
