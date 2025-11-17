// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Throttling;

/// <summary>
/// Centralized rate limiter for packet handlers.
/// Reuses a single <see cref="TokenBucketLimiter"/> per unique policy (RPS + Burst),
/// and uses composite endpoint keys "op:{opcode}|ep:{endpoint}" to isolate callers.
/// This avoids creating one limiter per endpoint, drastically reducing background tasks.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class PolicyRateLimiter
{
    #region Const

    private const System.Int32 MaxPolicies = 64;
    private const System.Int32 PolicyTtlSeconds = 1800; // 30 minutes
    private const System.Int32 SweepEveryNChecks = 1024;

    #endregion Const

    private static readonly System.Int32[] RpsTiers;
    private static readonly System.Int32[] BurstTiers;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, Entry> s_limiters;

    private static System.Int32 s_checkCounter;

    private readonly record struct Policy(System.Int32 Rps, System.Int32 Burst);

    private sealed class Entry
    {
        public System.Int64 LastUsedUtc;
        public TokenBucketLimiter Limiter { get; }
        public Entry(TokenBucketLimiter l) { Limiter = l; Touch(); }
        public void Touch() => LastUsedUtc = System.DateTime.UtcNow.Ticks;
    }

    static PolicyRateLimiter()
    {
        s_limiters = new();
        BurstTiers = [1, 2, 4, 8, 16, 32, 64];
        RpsTiers = [1, 2, 4, 8, 16, 32, 64, 128];
    }

    /// <summary>
    /// Try consuming a token for a given opcode and endpoint, under the supplied attribute policy.
    /// </summary>
    /// <param name="opCode">Handler opcode.</param>
    /// <param name="context"></param>
    /// <returns>Decision containing Allowed, RetryAfterMs, Credit, and Reason.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static TokenBucketLimiter.LimitDecision Check(System.UInt16 opCode, PacketContext<IPacket> context)
    {
        PacketRateLimitAttribute rl = context.Attributes.RateLimit!;
        if (rl.RequestsPerSecond <= 0)
        {
            return Allowed();
        }

        if (rl.Burst <= 0)
        {
            return DeniedHard();
        }

        // 1) Quantize policy
        System.Int32 rps = Quantize(rl.RequestsPerSecond, RpsTiers);
        System.Int32 burst = Quantize(rl.Burst, BurstTiers);
        Policy policy = new(rps, burst);

        // 2) Get or add limiter with hard cap
        TokenBucketLimiter limiter = GetOrAddLimiter(policy);
        TokenBucketLimiter.LimitDecision decision = limiter.Check(new CompositeEndpointKey(opCode, context.Connection.EndPoint));

        // 4) Opportunistic sweeping
        if ((System.Threading.Interlocked.Increment(ref s_checkCounter) & (SweepEveryNChecks - 1)) == 0)
        {
            SweepStale();
        }

        return decision;

        static System.Int32 Quantize(System.Int32 value, System.Int32[] tiers)
        {
            foreach (System.Int32 t in tiers)
            {
                if (value <= t)
                {
                    return t;
                }
            }

            return tiers[^1];
        }

        static TokenBucketLimiter.LimitDecision Allowed() => new()
        {
            Allowed = true,
            RetryAfterMs = 0,
            Credit = System.UInt16.MaxValue,
            Reason = TokenBucketLimiter.RateLimitReason.None
        };

        static TokenBucketLimiter.LimitDecision DeniedHard() => new()
        {
            Allowed = false,
            RetryAfterMs = System.Int32.MaxValue,
            Credit = 0,
            Reason = TokenBucketLimiter.RateLimitReason.HardLockout
        };
    }

    #region Private Methods

    private readonly struct CompositeEndpointKey(System.UInt16 op, IEndpointKey inner) : IEndpointKey, System.IEquatable<CompositeEndpointKey>
    {
        private readonly System.UInt16 _op = op;
        private readonly IEndpointKey _inner = inner;

        public System.String Address => $"op:{_op}|ep:{_inner.Address}";

        public override System.Int32 GetHashCode()
            => System.HashCode.Combine(_op, _inner);

        public override System.Boolean Equals(System.Object? obj)
            => obj is CompositeEndpointKey other && Equals(other);

        public System.Boolean Equals(CompositeEndpointKey obj)
            => _op == obj._op && Equals(_inner, obj._inner);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static TokenBucketLimiter GetOrAddLimiter(Policy policy)
    {
        // Fast path
        if (s_limiters.TryGetValue(policy, out var entry))
        {
            entry.Touch();
            return entry.Limiter;
        }

        // If over cap, map-nearest instead of creating a new one
        if (s_limiters.Count is >= MaxPolicies and > 0)
        {
            var nearest = FindNearestPolicy(policy);
            s_limiters[nearest].Touch();
            return s_limiters[nearest].Limiter;
        }

        // Create new limiter
        var opt = BuildOptions(policy);
        var created = new Entry(new TokenBucketLimiter(opt));
        var actual = s_limiters.GetOrAdd(policy, created);
        if (!ReferenceEquals(actual, created))
        {
            // Lost the race — dispose created if needed
            created.Limiter.Dispose();
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(PolicyRateLimiter)}] created policy-limiter " +
                                          $"rps={policy.Rps} burst={policy.Burst} total={s_limiters.Count}");
        }
        actual.Touch();
        return actual.Limiter;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Policy FindNearestPolicy(Policy wanted)
    {
        Policy nearest = default;
        System.Int32 best = System.Int32.MaxValue;
        foreach (Policy kvp in s_limiters.Keys)
        {
            System.Int32 dist = System.Math.Abs(kvp.Rps - wanted.Rps) + System.Math.Abs(kvp.Burst - wanted.Burst);
            if (dist < best)
            {
                best = dist; nearest = kvp; if (dist == 0)
                {
                    break;
                }
            }
        }
        return nearest;
    }

    private static TokenBucketOptions BuildOptions(Policy p)
    {
        // Reuse your s_defaults but apply p.Rps/p.Burst
        TokenBucketOptions d = s_defaults;
        return new TokenBucketOptions
        {
            CapacityTokens = p.Burst,
            RefillTokensPerSecond = p.Rps,
            TokenScale = d.TokenScale,
            ShardCount = d.ShardCount,
            HardLockoutSeconds = d.HardLockoutSeconds,
            StaleEntrySeconds = d.StaleEntrySeconds,
            CleanupIntervalSeconds = d.CleanupIntervalSeconds
        };
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void SweepStale()
    {
        System.Int64 now = System.DateTime.UtcNow.Ticks;
        foreach (var (policy, entry) in s_limiters)
        {
            System.Double ageSec = System.TimeSpan.FromTicks(now - entry.LastUsedUtc).TotalSeconds;
            if (ageSec > PolicyTtlSeconds)
            {
                _ = s_limiters.TryRemove(policy, out _);
                // Optionally dispose entry.Limiter if needed
            }
        }
    }

    // Reuse your existing default options loader:
    private static readonly TokenBucketOptions s_defaults = ConfigurationManager.Instance.Get<TokenBucketOptions>();

    #endregion Private Methods
}
