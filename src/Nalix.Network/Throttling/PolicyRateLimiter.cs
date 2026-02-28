// Copyright (c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Packets.Attributes;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Throttling;

/// <summary>
/// Centralized rate limiter for packet handlers.
/// Reuses a single <see cref="TokenBucketLimiter"/> per unique policy (RPS + Burst),
/// using composite endpoint keys to isolate callers per opcode.
/// Thread-safe with proper disposal coordination.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class PolicyRateLimiter
{
    #region Constants

    private const System.Int32 MaxPolicies = 64;
    private const System.Int32 PolicyTtlSeconds = 1800; // 30 minutes
    private const System.Int32 SweepEveryNChecks = 1024;

    #endregion Constants

    #region Fields

    private static System.Int32 s_checkCounter;
    private static volatile System.Boolean s_isShuttingDown;

    private static readonly System.Int32[] s_burstTiers = [1, 2, 4, 8, 16, 32, 64];
    private static readonly System.Int32[] s_rpsTiers = [1, 2, 4, 8, 16, 32, 64, 128];
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, Entry> s_limiters = new();
    private static readonly TokenBucketOptions s_defaults = ConfigurationManager.Instance.Get<TokenBucketOptions>();

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    #endregion Fields

    #region Private Types

    /// <summary>
    /// Wrapper for a rate limiter with usage tracking and disposal coordination.
    /// </summary>
    private sealed class Entry : System.IDisposable
    {
        private System.Int64 _lastUsedUtcTicks;
        private System.Int32 _activeUsers; // Reference counting for safe disposal
        private volatile System.Boolean _disposed;

        public TokenBucketLimiter Limiter { get; }

        public System.Int64 LastUsedUtcTicks =>
            System.Threading.Interlocked.Read(ref _lastUsedUtcTicks);

        public Entry(TokenBucketLimiter limiter)
        {
            Limiter = limiter ?? throw new System.ArgumentNullException(nameof(limiter));
            _activeUsers = 0;
            _disposed = false;
            Touch();
        }

        /// <summary>
        /// Updates last used timestamp.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            System.Threading.Interlocked.Exchange(
                ref _lastUsedUtcTicks,
                System.DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// Attempts to acquire usage reference.  Returns false if disposed.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean TryAcquire()
        {
            if (_disposed)
            {
                return false;
            }

            _ = System.Threading.Interlocked.Increment(ref _activeUsers);

            // Double-check after increment
            if (_disposed)
            {
                _ = System.Threading.Interlocked.Decrement(ref _activeUsers);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Releases usage reference.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Release() => _ = System.Threading.Interlocked.Decrement(ref _activeUsers);

        /// <summary>
        /// Checks if entry is stale (not used for TTL duration).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean IsStale(System.Int64 nowTicks, System.Int32 ttlSeconds)
        {
            System.Double ageSec = System.TimeSpan
                .FromTicks(nowTicks - LastUsedUtcTicks)
                .TotalSeconds;

            return ageSec > ttlSeconds;
        }

        /// <summary>
        /// Safely disposes the limiter after waiting for active users.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Wait briefly for active users to complete
            System.Int32 waited = 0;
            System.Int32 spinCount = 0;
            const System.Int32 maxWaitMs = 100;

            while (System.Threading.Interlocked.CompareExchange(ref _activeUsers, 0, 0) > 0
                   && waited < maxWaitMs)
            {
                if (spinCount++ < 10)
                {
                    System.Threading.Thread.SpinWait(100);
                }
                else
                {
                    System.Threading.Thread.Sleep(1);
                    waited++;
                }
            }

            // Dispose limiter
            try
            {
                Limiter.Dispose();
            }
            catch (System.Exception ex)
            {
                s_logger?.Error($"[NW. {nameof(PolicyRateLimiter)}:Entry] " +
                               $"disposal-error msg={ex.Message}");
            }
        }
    }

    /// <summary>
    /// Policy key representing RPS and burst capacity.
    /// </summary>
    private readonly record struct Policy(System.Int32 Rps, System.Int32 Burst);

    /// <summary>
    /// Composite endpoint key combining opcode and network endpoint.
    /// </summary>
    private readonly struct RateLimitSubject(System.UInt16 op, INetworkEndpoint inner)
        : INetworkEndpoint, System.IEquatable<RateLimitSubject>
    {
        private readonly System.UInt16 _op = op;
        private readonly INetworkEndpoint _inner = inner ??
            throw new System.ArgumentNullException(nameof(inner));

        public System.String Address => $"op:{_op}|ep:{_inner.Address}";

        public override System.Int32 GetHashCode() =>
            System.HashCode.Combine(_op, _inner);

        public System.Boolean Equals(RateLimitSubject other) =>
            _op == other._op && Equals(_inner, other._inner);

        public override System.Boolean Equals(System.Object obj) =>
            obj is RateLimitSubject other && Equals(other);

        public static System.Boolean operator ==(RateLimitSubject left, RateLimitSubject right) =>
            left.Equals(right);

        public static System.Boolean operator !=(RateLimitSubject left, RateLimitSubject right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Result of rate limit check attempt.
    /// </summary>
    private readonly struct CheckResult
    {
        public TokenBucketLimiter.RateLimitDecision Decision { get; init; }
        public System.Boolean Success { get; init; }
    }

    #endregion Private Types

    #region Public API

    /// <summary>
    /// Checks rate limit for a packet handler invocation.
    /// </summary>
    /// <param name="opCode">Handler operation code.</param>
    /// <param name="context">Packet processing context.</param>
    /// <returns>Rate limit decision with allow/deny status and retry information.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when context is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static TokenBucketLimiter.RateLimitDecision Check(
        System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<IPacket> context)
    {
        System.ArgumentNullException.ThrowIfNull(context);

        // Early exit if shutting down
        if (s_isShuttingDown)
        {
            return CREATE_DENIED_DECISION(isHard: true);
        }

        // Validate rate limit attribute
        CheckResult validationResult = VALIDATE_RATE_LIMIT_ATTRIBUTE(context);
        if (!validationResult.Success)
        {
            return validationResult.Decision;
        }

        // Extract and quantize policy
        Policy policy = EXTRACT_AND_QUANTIZE_POLICY(context.Attributes.RateLimit!);

        // Perform rate limit check
        CheckResult checkResult = PERFORM_RATE_LIMIT_CHECK(opCode, context, policy);

        // Opportunistic cleanup
        TRY_SCHEDULE_SWEEP();

        return checkResult.Decision;
    }

    /// <summary>
    /// Disposes all policy limiters and prevents new checks.
    /// Should be called during application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        s_isShuttingDown = true;

        System.Int32 disposedCount = 0;
        System.Int32 totalCount = s_limiters.Count;

        // Take snapshot to avoid modification during iteration
        var snapshot = s_limiters.ToArray();

        foreach (var (policy, _) in snapshot)
        {
            if (s_limiters.TryRemove(policy, out Entry removed))
            {
                try
                {
                    removed.Dispose();
                    disposedCount++;
                }
                catch (System.Exception ex)
                {
                    s_logger?.Error($"[NW.{nameof(PolicyRateLimiter)}:{nameof(Shutdown)}] disposal-error policy={policy} msg={ex.Message}");
                }
            }
        }

        s_logger?.Info($"[NW.{nameof(PolicyRateLimiter)}:{nameof(Shutdown)}] disposed={disposedCount}/{totalCount}");
    }

    #endregion Public API

    #region Validation

    /// <summary>
    /// Validates rate limit attribute and returns early decision if invalid.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static CheckResult VALIDATE_RATE_LIMIT_ATTRIBUTE(PacketContext<IPacket> context)
    {
        PacketRateLimitAttribute rl = context.Attributes.RateLimit;

        // ✅ FIX: Proper null check instead of null-forgiving operator
        if (rl is null)
        {
            return new CheckResult
            {
                Success = true,
                Decision = CREATE_ALLOWED_DECISION()
            };
        }

        // Invalid RPS = allow (no rate limiting)
        if (rl.RequestsPerSecond <= 0)
        {
            return new CheckResult
            {
                Success = true,
                Decision = CREATE_ALLOWED_DECISION()
            };
        }

        // Zero burst = hard deny
        if (rl.Burst <= 0)
        {
            s_logger?.Warn($"[NW.{nameof(PolicyRateLimiter)}] " +
                          $"invalid-burst burst={rl.Burst} - denying request");

            return new CheckResult
            {
                Success = false,
                Decision = CREATE_DENIED_DECISION(isHard: true)
            };
        }

        return new CheckResult { Success = true };
    }

    #endregion Validation

    #region Policy Management

    /// <summary>
    /// Extracts rate limit policy and quantizes to predefined tiers.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static Policy EXTRACT_AND_QUANTIZE_POLICY(PacketRateLimitAttribute rl)
    {
        System.Int32 rps = QUANTIZE_VALUE(rl.RequestsPerSecond, s_rpsTiers);
        System.Int32 burst = QUANTIZE_VALUE(rl.Burst, s_burstTiers);

        return new Policy(rps, burst);
    }

    /// <summary>
    /// Quantizes a value to the nearest tier (rounding up).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 QUANTIZE_VALUE(System.Int32 value, System.Int32[] tiers)
    {
        foreach (System.Int32 tier in tiers)
        {
            if (value <= tier)
            {
                return tier;
            }
        }

        // Value exceeds max tier - clamp to maximum
        return tiers[^1];
    }

    /// <summary>
    /// Creates token bucket options for a specific policy.
    /// </summary>
    private static TokenBucketOptions CREATE_OPTIONS_FOR_POLICY(Policy policy)
    {
        return new TokenBucketOptions
        {
            CapacityTokens = policy.Burst,
            RefillTokensPerSecond = policy.Rps,
            TokenScale = s_defaults.TokenScale,
            ShardCount = s_defaults.ShardCount,
            HardLockoutSeconds = s_defaults.HardLockoutSeconds,
            StaleEntrySeconds = s_defaults.StaleEntrySeconds,
            CleanupIntervalSeconds = s_defaults.CleanupIntervalSeconds,
            MaxTrackedEndpoints = s_defaults.MaxTrackedEndpoints,
            MaxSoftViolations = s_defaults.MaxSoftViolations,
            SoftViolationWindowSeconds = s_defaults.SoftViolationWindowSeconds
        };
    }

    #endregion Policy Management

    #region Rate Limit Check

    /// <summary>
    /// Performs the actual rate limit check using appropriate limiter.
    /// </summary>
    private static CheckResult PERFORM_RATE_LIMIT_CHECK(System.UInt16 opCode, PacketContext<IPacket> context, Policy policy)
    {
        // Validate connection endpoint
        if (context.Connection?.EndPoint is null)
        {
            s_logger?.Warn($"[NW.{nameof(PolicyRateLimiter)}] " +
                          $"missing-endpoint opCode={opCode}");

            return new CheckResult
            {
                Success = false,
                Decision = CREATE_DENIED_DECISION(isHard: false)
            };
        }

        // Get or create limiter for policy
        Entry entry = GET_OR_CREATE_LIMITER_ENTRY(policy);

        // Try acquire reference for safe usage
        if (!entry.TryAcquire())
        {
            // Entry is being disposed - deny with retry
            return new CheckResult
            {
                Success = false,
                Decision = CREATE_DENIED_DECISION(isHard: false, retryAfterMs: 1000)
            };
        }

        try
        {
            // Perform check with composite subject
            var subject = new RateLimitSubject(opCode, context.Connection.EndPoint);
            var decision = entry.Limiter.Check(subject);

            return new CheckResult
            {
                Success = true,
                Decision = decision
            };
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>
    /// Gets existing or creates new limiter entry for policy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Entry GET_OR_CREATE_LIMITER_ENTRY(Policy policy)
    {
        // Fast path:  existing entry
        if (s_limiters.TryGetValue(policy, out Entry existingEntry))
        {
            existingEntry.Touch();
            return existingEntry;
        }

        // Check if at capacity - reuse closest policy
        if (IS_AT_POLICY_CAPACITY())
        {
            Entry reusedEntry = TRY_REUSE_CLOSEST_POLICY(policy);
            if (reusedEntry is not null)
            {
                return reusedEntry;
            }
        }

        // Create new entry
        return CREATE_NEW_LIMITER_ENTRY(policy);
    }

    /// <summary>
    /// Checks if policy cache is at capacity.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IS_AT_POLICY_CAPACITY() => s_limiters.Count >= MaxPolicies;

    /// <summary>
    /// Attempts to reuse the closest existing policy when at capacity.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Entry TRY_REUSE_CLOSEST_POLICY(Policy wanted)
    {
        Policy closest = FIND_CLOSEST_POLICY(wanted);

        // ✅ FIX: Check if policy still exists (may have been removed)
        if (s_limiters.TryGetValue(closest, out Entry reused))
        {
            reused.Touch();

            s_logger?.Debug($"[NW.{nameof(PolicyRateLimiter)}] " +
                           $"reusing-policy wanted=({wanted.Rps},{wanted.Burst}) " +
                           $"closest=({closest.Rps},{closest.Burst})");

            return reused;
        }

        return null;
    }

    /// <summary>
    /// Finds the closest existing policy to the wanted policy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Policy FIND_CLOSEST_POLICY(Policy wanted)
    {
        Policy closest = default;
        System.Int32 bestDistance = System.Int32.MaxValue;

        foreach (Policy candidate in s_limiters.Keys)
        {
            System.Int32 distance = CALCULATE_POLICY_DISTANCE(candidate, wanted);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;

                if (distance == 0)
                {
                    break; // Exact match
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// Calculates Manhattan distance between two policies.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 CALCULATE_POLICY_DISTANCE(Policy a, Policy b) => System.Math.Abs(a.Rps - b.Rps) + System.Math.Abs(a.Burst - b.Burst);

    /// <summary>
    /// Creates a new limiter entry for policy with proper race handling.
    /// </summary>
    private static Entry CREATE_NEW_LIMITER_ENTRY(Policy policy)
    {
        TokenBucketOptions options = CREATE_OPTIONS_FOR_POLICY(policy);
        Entry newEntry = new(new TokenBucketLimiter(options));

        // Try add - handle race condition
        Entry actualEntry = s_limiters.GetOrAdd(policy, newEntry);

        if (ReferenceEquals(actualEntry, newEntry))
        {
            // Successfully added
            s_logger?.Info($"[NW.{nameof(PolicyRateLimiter)}] ]created-policy-limiter rps={policy.Rps} burst={policy.Burst} total={s_limiters.Count}");
        }
        else
        {
            // Lost race - dispose the one we created
            newEntry.Dispose();
        }

        actualEntry.Touch();
        return actualEntry;
    }

    #endregion Rate Limit Check

    #region Decision Helpers

    /// <summary>
    /// Creates an allowed decision.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static TokenBucketLimiter.RateLimitDecision CREATE_ALLOWED_DECISION()
    {
        return new TokenBucketLimiter.RateLimitDecision
        {
            Allowed = true,
            RetryAfterMs = 0,
            Credit = System.UInt16.MaxValue,
            Reason = TokenBucketLimiter.RateLimitReason.None
        };
    }

    /// <summary>
    /// Creates a denied decision.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static TokenBucketLimiter.RateLimitDecision CREATE_DENIED_DECISION(
        System.Boolean isHard,
        System.Int32 retryAfterMs = 0)
    {
        return new TokenBucketLimiter.RateLimitDecision
        {
            Allowed = false,
            RetryAfterMs = retryAfterMs > 0 ? retryAfterMs : (isHard ? System.Int32.MaxValue : 1000),
            Credit = 0,
            Reason = isHard
                ? TokenBucketLimiter.RateLimitReason.HardLockout
                : TokenBucketLimiter.RateLimitReason.SoftThrottle
        };
    }

    #endregion Decision Helpers

    #region Cleanup

    /// <summary>
    /// Tries to schedule a sweep operation if counter threshold is reached.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void TRY_SCHEDULE_SWEEP()
    {
        // Use unchecked to allow overflow without exception
        unchecked
        {
            System.Int32 count = System.Threading.Interlocked.Increment(ref s_checkCounter);

            if ((count & (SweepEveryNChecks - 1)) == 0)
            {
                EVICTS_TALE_POLICIES();
            }
        }
    }

    /// <summary>
    /// Evicts stale policy entries that haven't been used recently.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void EVICTS_TALE_POLICIES()
    {
        if (s_isShuttingDown)
        {
            return;
        }

        System.Int64 nowTicks = System.DateTime.UtcNow.Ticks;
        System.Int32 evictedCount = 0;

        foreach (var (policy, entry) in s_limiters)
        {
            if (entry.IsStale(nowTicks, PolicyTtlSeconds))
            {
                if (s_limiters.TryRemove(policy, out Entry removed))
                {
                    removed.Dispose();
                    evictedCount++;
                }
            }
        }

        if (evictedCount > 0)
        {
            s_logger?.Debug($"[NW.{nameof(PolicyRateLimiter)}] " +
                           $"evicted-stale-policies count={evictedCount} remaining={s_limiters.Count}");
        }
    }

    #endregion Cleanup
}