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

    #region Fields

    private static System.Int32 s_checkCounter;

    private static readonly System.Int32[] s_burstTiers = [1, 2, 4, 8, 16, 32, 64];
    private static readonly System.Int32[] s_rpsTiers = [1, 2, 4, 8, 16, 32, 64, 128];
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, Entry> s_limiters = new();
    private static readonly TokenBucketOptions s_defaults = ConfigurationManager.Instance.Get<TokenBucketOptions>();

    #endregion Fields

    #region Private Types

    private sealed class Entry
    {
        #region Fields

        private System.Int64 _lastUsedUtcTicks;

        #endregion Fields

        #region Properties

        public TokenBucketLimiter Limiter { get; }

        public System.Int64 LastUsedUtcTicks => System.Threading.Interlocked.Read(ref _lastUsedUtcTicks);

        #endregion Properties

        public Entry(TokenBucketLimiter limiter)
        {
            Limiter = limiter ?? throw new System.ArgumentNullException(nameof(limiter));
            this.Touch();
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Touch() => System.Threading.Interlocked.Exchange(ref _lastUsedUtcTicks, System.DateTime.UtcNow.Ticks);
    }

    private readonly record struct Policy(System.Int32 Rps, System.Int32 Burst);

    #endregion Private Types

    #region Public API

    /// <summary>
    /// Try consuming a token for a given opcode and endpoint, under the supplied attribute policy.
    /// </summary>
    /// <param name="opCode">Handler opcode.</param>
    /// <param name="context"></param>
    /// <returns>Decision containing Allowed, RetryAfterMs, Credit, and Reason.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static TokenBucketLimiter.RateLimitDecision Check(
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<IPacket> context)
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
        System.Int32 rps = Quantize(rl.RequestsPerSecond, s_rpsTiers);
        System.Int32 burst = Quantize(rl.Burst, s_burstTiers);
        Policy policy = new(rps, burst);

        // 2) Get or add limiter with hard cap
        TokenBucketLimiter limiter = GetOrAddLimiter(policy);
        TokenBucketLimiter.RateLimitDecision decision = limiter.Check(new RateLimitSubject(opCode, context.Connection.EndPoint));

        // 4) Opportunistic sweeping
        if ((System.Threading.Interlocked.Increment(ref s_checkCounter) & (SweepEveryNChecks - 1)) == 0)
        {
            EvictStalePolicies();
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

        static TokenBucketLimiter.RateLimitDecision Allowed() => new()
        {
            Allowed = true,
            RetryAfterMs = 0,
            Credit = System.UInt16.MaxValue,
            Reason = TokenBucketLimiter.RateLimitReason.None
        };

        static TokenBucketLimiter.RateLimitDecision DeniedHard() => new()
        {
            Allowed = false,
            RetryAfterMs = System.Int32.MaxValue,
            Credit = 0,
            Reason = TokenBucketLimiter.RateLimitReason.HardLockout
        };
    }

    /// <summary>
    /// Disposes all existing policy limiters and clears the internal cache.
    /// Should be called on application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        foreach ((Policy policy, Entry _) in s_limiters)
        {
            if (s_limiters.TryRemove(policy, out Entry removed))
            {
                try
                {
                    removed.Limiter.Dispose();
                }
                catch
                {
                    // Swallow: shutdown path
                }
            }
        }
    }

    #endregion Public API

    #region Private Methods

    private readonly struct RateLimitSubject(System.UInt16 op, INetworkEndpoint inner) : INetworkEndpoint, System.IEquatable<RateLimitSubject>
    {
        private readonly System.UInt16 _op = op;
        private readonly INetworkEndpoint _inner = inner;

        public System.String Address => $"op:{_op}|ep:{_inner.Address}";

        public override System.Int32 GetHashCode() => System.HashCode.Combine(_op, _inner);

        public System.Boolean Equals(RateLimitSubject obj) => _op == obj._op && Equals(_inner, obj._inner);

        public override System.Boolean Equals(System.Object obj) => obj is RateLimitSubject other && Equals(other);
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
            Policy nearest = SelectClosestPolicy(policy);
            if (s_limiters.TryGetValue(nearest, out var reused))
            {
                reused.Touch();
                return reused.Limiter;
            }
        }

        // Create new limiter
        TokenBucketOptions opt = CreateOptionsForPolicy(policy);
        Entry created = new(new TokenBucketLimiter(opt));
        Entry actual = s_limiters.GetOrAdd(policy, created);
        if (!ReferenceEquals(actual, created))
        {
            // Lost the race — dispose created if needed
            created.Limiter.Dispose();
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[NW.{nameof(PolicyRateLimiter)}:{nameof(GetOrAddLimiter)}] created policy-limiter " +
                                          $"rps={policy.Rps} burst={policy.Burst} total={s_limiters.Count}");
        }
        actual.Touch();
        return actual.Limiter;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Policy SelectClosestPolicy(Policy wanted)
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

    private static TokenBucketOptions CreateOptionsForPolicy(Policy p)
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
    private static void EvictStalePolicies()
    {
        System.Int64 now = System.DateTime.UtcNow.Ticks;
        foreach (var (policy, entry) in s_limiters)
        {
            System.Double ageSec = System.TimeSpan.FromTicks(now - entry.LastUsedUtcTicks).TotalSeconds;

            if (ageSec > PolicyTtlSeconds)
            {
                if (s_limiters.TryRemove(policy, out var removed))
                {
                    removed.Limiter.Dispose();
                }
            }
        }
    }

    #endregion Private Methods
}
