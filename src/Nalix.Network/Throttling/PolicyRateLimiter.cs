// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Internal.Net;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Throttling;

/// <summary>
/// Centralized rate limiter for packet handlers.
/// Reuses a single <see cref="TokenBucketLimiter"/> per unique policy (RPS + Burst),
/// and uses composite endpoint keys "op:{opcode}|ep:{endpoint}" to isolate callers.
/// This avoids creating one limiter per endpoint, drastically reducing background tasks.
/// </summary>
public static class PolicyRateLimiter
{
    #region Const

    private const System.Int32 MaxPolicies = 64;
    private const System.Int32 PolicyTtlSeconds = 1800; // 30 minutes
    private const System.Int32 SweepEveryNChecks = 1024;

    #endregion Const

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, Entry> s_limiters = new();
    private static readonly System.Int32[] RpsTiers = [1, 2, 4, 8, 16, 32, 64, 128];
    private static readonly System.Int32[] BurstTiers = [1, 2, 4, 8, 16, 32, 64];
    private static System.Int32 s_checkCounter;

    private readonly record struct Policy(System.Int32 Rps, System.Int32 Burst);

    private sealed class Entry
    {
        public TokenBucketLimiter Limiter { get; }
        public System.Int64 LastUsedUtc;
        public Entry(TokenBucketLimiter l) { Limiter = l; Touch(); }
        public void Touch() => LastUsedUtc = System.DateTime.UtcNow.Ticks;
    }

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
            return Allowed();
        }

        if (attr.Burst <= 0)
        {
            return DeniedHard();
        }

        // 1) Quantize policy
        System.Int32 rps = Quantize(attr.RequestsPerSecond, RpsTiers);
        System.Int32 burst = Quantize(attr.Burst, BurstTiers);
        var policy = new Policy(rps, burst);

        // 2) Get or add limiter with hard cap
        var limiter = GetOrAddLimiter(policy);

        // 3) Compose key per opcode + endpoint
        System.String key = $"op:{opCode}|ep:{ip}";
        var decision = limiter.Check(IxCP9(key));

        // 4) Opportunistic sweeping
        if ((System.Threading.Interlocked.Increment(ref s_checkCounter) & (SweepEveryNChecks - 1)) == 0)
        {
            SweepStale();
        }

        return decision;

        static System.Int32 Quantize(System.Int32 value, System.Int32[] tiers)
        {
            foreach (var t in tiers)
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static IPAddressKey IxCP9(System.String s)
    {
        unchecked
        {
            System.UInt64 h = 1469598103934665603UL;
            for (System.Int32 i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 1099511628211UL;
            }

            System.Byte[] bytes = System.BitConverter.GetBytes(h);
            System.Net.IPAddress ip = new(bytes.Length == 16 ? bytes : bytes.Length == 8
                ? [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, bytes[0], bytes[1], bytes[2], bytes[3]]
                : new System.Byte[16]);

            return IPAddressKey.FromIpAddress(ip);
        }
    }


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

    private static Policy FindNearestPolicy(Policy wanted)
    {
        Policy nearest = default;
        System.Int32 best = System.Int32.MaxValue;
        foreach (var kvp in s_limiters.Keys)
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
        var d = s_defaults;
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

    private static void SweepStale()
    {
        System.Int64 now = System.DateTime.UtcNow.Ticks;
        foreach (var (policy, entry) in s_limiters)
        {
            var ageSec = System.TimeSpan.FromTicks(now - entry.LastUsedUtc).TotalSeconds;
            if (ageSec > PolicyTtlSeconds)
            {
                _ = s_limiters.TryRemove(policy, out _);
                // Optionally dispose entry.Limiter if needed
            }
        }
    }

    // Reuse your existing default options loader:
    private static readonly TokenBucketOptions s_defaults =
        ConfigurationManager.Instance.Get<TokenBucketOptions>();

    #endregion Private Methods
}
