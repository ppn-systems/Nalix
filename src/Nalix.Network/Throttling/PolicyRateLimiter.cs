// Copyright (c) 2025-2026 PPN Corporation. All rights reserved. 

using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Packets.Attributes;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch;
using System.Linq;

namespace Nalix.Network.Throttling;

/// <summary>
/// Provides a policy-based rate limiting mechanism using token bucket algorithms.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PolicyRateLimiter"/> manages multiple rate limit policies defined by
/// requests-per-second (RPS) and burst capacity. Policies are quantized into predefined tiers
/// to reduce memory usage and improve reuse.
/// </para>
/// <para>
/// Each policy is backed by a shared <see cref="TokenBucketLimiter"/> instance and
/// automatically evicted when unused for a configured period.
/// </para>
/// <para>
/// This class is thread-safe and optimized for high-throughput network environments.
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class PolicyRateLimiter : IReportable, System.IDisposable
{
    #region Constants

    private const System.Int32 MaxPolicies = 64;
    private const System.Int32 PolicyTtlSeconds = 1800;
    private const System.Int32 SweepEveryNChecks = 1024;

    #endregion Constants

    #region Fields

    private System.Int32 _checkCounter;
    private System.Int32 _disposed;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, Entry> _limiters = new();

    private static readonly System.Int32[] s_burstTiers = [1, 2, 4, 8, 16, 32, 64];
    private static readonly System.Int32[] s_rpsTiers = [1, 2, 4, 8, 16, 32, 64, 128];

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly TokenBucketOptions s_defaults = ConfigurationManager.Instance.Get<TokenBucketOptions>();

    #endregion Fields

    #region Private Types

    private sealed class Entry : System.IDisposable
    {
        private System.Int64 _lastUsedUtcTicks;
        private System.Int32 _activeUsers;
        private System.Int32 _disposed;

        public TokenBucketLimiter Limiter { get; }

        public System.Int64 LastUsedUtcTicks =>
            System.Threading.Interlocked.Read(ref _lastUsedUtcTicks);

        public Entry(TokenBucketLimiter limiter)
        {
            Limiter = limiter ?? throw new System.ArgumentNullException(nameof(limiter));
            _activeUsers = 0;
            _disposed = 0;
            Touch();
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            System.Threading.Interlocked.Exchange(
                ref _lastUsedUtcTicks,
                System.DateTime.UtcNow.Ticks);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean TryAcquire()
        {
            if (System.Threading.Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            System.Int32 newCount = System.Threading.Interlocked.Increment(ref _activeUsers);

            if (System.Threading.Volatile.Read(ref _disposed) != 0)
            {
                System.Threading.Interlocked.Decrement(ref _activeUsers);
                return false;
            }

            if (newCount <= 0)
            {
                System.Threading.Interlocked.Decrement(ref _activeUsers);
                s_logger?.Error($"[NW.{nameof(PolicyRateLimiter)}:Entry] active-users-overflow");
                return false;
            }

            return true;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            System.Int32 remaining = System.Threading.Interlocked.Decrement(ref _activeUsers);

            if (remaining < 0)
            {
                s_logger?.Error($"[NW.{nameof(PolicyRateLimiter)}:Entry] active-users-overflow");
                System.Threading.Interlocked.Exchange(ref _activeUsers, 0);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean IsStale(System.Int64 nowTicks, System.Int32 ttlSeconds)
        {
            if (System.Threading.Volatile.Read(ref _disposed) != 0)
            {
                return true;
            }

            System.Int64 lastTicks = System.Threading.Interlocked.Read(ref _lastUsedUtcTicks);
            System.Double ageSec = System.TimeSpan.FromTicks(nowTicks - lastTicks).TotalSeconds;

            return ageSec > ttlSeconds;
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            System.Int32 waitedMs = 0;
            System.Int32 backoffMs = 1;
            const System.Int32 maxWaitMs = 500;
            const System.Int32 maxBackoffMs = 50;

            while (System.Threading.Volatile.Read(ref _activeUsers) > 0 && waitedMs < maxWaitMs)
            {
                System.Threading.Thread.Sleep(backoffMs);
                waitedMs += backoffMs;
                backoffMs = System.Math.Min(backoffMs * 2, maxBackoffMs);
            }

            System.Int32 remainingUsers = System.Threading.Volatile.Read(ref _activeUsers);

            try
            {
                Limiter.Dispose();
            }
            catch (System.Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(PolicyRateLimiter)}:Entry] disposal-error", ex);
            }
        }
    }

    private readonly record struct Policy(System.Int32 Rps, System.Int32 Burst);

    private readonly struct RateLimitSubject : INetworkEndpoint, System.IEquatable<RateLimitSubject>
    {
        private readonly System.UInt16 _op;
        private readonly INetworkEndpoint _inner;

        public RateLimitSubject(System.UInt16 op, INetworkEndpoint inner)
        {
            _op = op;
            _inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
        }

        public System.String Address => $"op:{_op:X4}|ep:{_inner.Address}";

        public override System.Int32 GetHashCode()
        {
            unchecked
            {
                System.Int32 hash = 17;
                hash = (hash * 31) + _op.GetHashCode();
                hash = (hash * 31) + (_inner?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public System.Boolean Equals(RateLimitSubject other)
        {
            return _op == other._op &&
                   (ReferenceEquals(_inner, other._inner) ||
                    (_inner?.Equals(other._inner) ?? (other._inner is null)));
        }

        public override System.Boolean Equals(System.Object obj) => obj is RateLimitSubject other && Equals(other);

        public static System.Boolean operator ==(RateLimitSubject left, RateLimitSubject right)
            => left.Equals(right);

        public static System.Boolean operator !=(RateLimitSubject left, RateLimitSubject right)
            => !left.Equals(right);

        public override System.String ToString() => Address;
    }

    private readonly struct CheckResult
    {
        public TokenBucketLimiter.RateLimitDecision Decision { get; init; }
        public System.Boolean Success { get; init; }
    }

    #endregion Private Types

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyRateLimiter"/> class.
    /// </summary>
    /// <remarks>
    /// Default rate limiting options are loaded from configuration at startup.
    /// </remarks>
    public PolicyRateLimiter()
    {
        _checkCounter = 0;
        _disposed = 0;

        s_logger?.Debug($"[NW.{nameof(PolicyRateLimiter)}] initialized");
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Performs a rate limit check for the specified operation code and packet context.
    /// </summary>
    /// <param name="opCode">
    /// The operation code associated with the incoming packet.
    /// </param>
    /// <param name="context">
    /// The packet context containing connection, endpoint, and rate limit metadata.
    /// </param>
    /// <returns>
    /// A <see cref="TokenBucketLimiter.RateLimitDecision"/> indicating whether the request
    /// is allowed, throttled, or denied.
    /// </returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when the limiter has been disposed.</exception>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// If no rate limit attribute is present, the request is always allowed.
    /// </para>
    /// <para>
    /// Rate limit policies are shared and reused across requests with similar limits.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public TokenBucketLimiter.RateLimitDecision Check(System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<IPacket> context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        System.ObjectDisposedException.ThrowIf(_disposed != 0, this);

        CheckResult validationResult = VALIDATE_RATE_LIMIT_ATTRIBUTE(context);
        if (!validationResult.Success)
        {
            return validationResult.Decision;
        }

        Policy policy = EXTRACT_AND_QUANTIZE_POLICY(context.Attributes.RateLimit!);

        CheckResult checkResult = PERFORM_RATE_LIMIT_CHECK(opCode, context, policy);

        TRY_SCHEDULE_SWEEP();

        return checkResult.Decision;
    }

    /// <summary>
    /// Generates a human-readable diagnostic report describing the current rate limiter state.
    /// </summary>
    /// <returns>
    /// A formatted string containing active policies, usage counters,
    /// and last-access timestamps.
    /// </returns>
    /// <remarks>
    /// This method is intended for diagnostics, monitoring, and debugging purposes.
    /// </remarks>
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();

        sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PolicyRateLimiter Status:");
        sb.AppendLine($"Active Policies    : {_limiters.Count}/{MaxPolicies}");
        sb.AppendLine($"Check Counter      : {System.Threading.Volatile.Read(ref _checkCounter):N0}");
        sb.AppendLine();

        if (_limiters.IsEmpty)
        {
            sb.AppendLine("(no active policies)");
            return sb.ToString();
        }

        sb.AppendLine("Active Policies:");
        sb.AppendLine("------------------------------------------------------------");
        sb.AppendLine("RPS  | Burst | Last Used (UTC)");
        sb.AppendLine("------------------------------------------------------------");

        var sorted = _limiters.OrderByDescending(kv => kv.Key.Rps)
                              .ThenByDescending(kv => kv.Key.Burst)
                              .ToList();

        foreach (var (policy, entry) in sorted)
        {
            System.Int64 lastTicks = entry.LastUsedUtcTicks;
            System.DateTime lastUsed = new(lastTicks, System.DateTimeKind.Utc);

            sb.AppendLine(
                $"{policy.Rps,4} | {policy.Burst,5} | {lastUsed:yyyy-MM-dd HH:mm:ss}");
        }

        sb.AppendLine("------------------------------------------------------------");

        return sb.ToString();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="PolicyRateLimiter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All active policy limiters are disposed and removed.
    /// </para>
    /// <para>
    /// This method is safe to call multiple times.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        System.Int32 disposedCount = 0;
        System.Int32 totalCount = _limiters.Count;

        System.Int32 maxAttempts = 10;
        System.Int32 attempt = 0;

        while (_limiters.Count > 0 && attempt++ < maxAttempts)
        {
            foreach (var (policy, _) in _limiters)
            {
                if (_limiters.TryRemove(policy, out Entry removed))
                {
                    try
                    {
                        removed.Dispose();
                        disposedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        s_logger?.Error(
                            $"[NW.{nameof(PolicyRateLimiter)}:{nameof(Dispose)}] " +
                            $"disposal-error policy={policy}", ex);
                    }
                }
            }

            if (_limiters.Count > 0)
            {
                System.Threading.Thread.Sleep(50);
            }
        }

        if (_limiters.Count > 0)
        {
            _limiters.Clear();
        }

        s_logger?.Info($"[NW.{nameof(PolicyRateLimiter)}:{nameof(Dispose)}] disposed={disposedCount}/{totalCount}");

        System.GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Validation

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static CheckResult VALIDATE_RATE_LIMIT_ATTRIBUTE(PacketContext<IPacket> context)
    {
        PacketRateLimitAttribute rl = context.Attributes.RateLimit;

        if (rl is null)
        {
            return new CheckResult
            {
                Success = true,
                Decision = CREATE_ALLOWED_DECISION()
            };
        }

        if (rl.RequestsPerSecond <= 0)
        {
            return new CheckResult
            {
                Success = true,
                Decision = CREATE_ALLOWED_DECISION()
            };
        }

        if (rl.Burst <= 0)
        {
            s_logger?.Warn($"[NW.{nameof(PolicyRateLimiter)}] invalid-burst burst={rl.Burst}");

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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Policy EXTRACT_AND_QUANTIZE_POLICY(PacketRateLimitAttribute rl)
    {
        System.Int32 rps = QUANTIZE_VALUE(rl.RequestsPerSecond, s_rpsTiers);
        System.Int32 burst = QUANTIZE_VALUE(rl.Burst, s_burstTiers);

        return new Policy(rps, burst);
    }

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

        return tiers[^1];
    }

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
            SoftViolationWindowSeconds = s_defaults.SoftViolationWindowSeconds,
            InitialTokens = s_defaults.InitialTokens
        };
    }

    #endregion Policy Management

    #region Rate Limit Check

    private CheckResult PERFORM_RATE_LIMIT_CHECK(System.UInt16 opCode, PacketContext<IPacket> context, Policy policy)
    {
        if (context.Connection?.EndPoint is null)
        {
            s_logger?.Warn($"[NW.{nameof(PolicyRateLimiter)}] missing-endpoint opCode={opCode}");

            return new CheckResult
            {
                Success = false,
                Decision = CREATE_DENIED_DECISION(isHard: false)
            };
        }

        Entry entry = GET_OR_CREATE_LIMITER_ENTRY(policy);

        if (!entry.TryAcquire())
        {
            return new CheckResult
            {
                Success = false,
                Decision = CREATE_DENIED_DECISION(isHard: false, retryAfterMs: 1000)
            };
        }

        try
        {
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private Entry GET_OR_CREATE_LIMITER_ENTRY(Policy policy)
    {
        if (_limiters.TryGetValue(policy, out Entry existingEntry))
        {
            existingEntry.Touch();
            return existingEntry;
        }

        if (IS_AT_POLICY_CAPACITY())
        {
            Entry reusedEntry = TRY_REUSE_CLOSEST_POLICY(policy);
            if (reusedEntry is not null)
            {
                return reusedEntry;
            }
        }

        return CREATE_NEW_LIMITER_ENTRY(policy);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean IS_AT_POLICY_CAPACITY() => _limiters.Count >= MaxPolicies;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private Entry TRY_REUSE_CLOSEST_POLICY(Policy wanted)
    {
        if (_limiters.IsEmpty)
        {
            return null;
        }

        Policy closest = FIND_CLOSEST_POLICY(wanted);

        if (closest.Equals(default))
        {
            return null;
        }

        if (_limiters.TryGetValue(closest, out Entry reused))
        {
            reused.Touch();

            s_logger?.Debug(
                $"[NW.{nameof(PolicyRateLimiter)}] reusing-policy " +
                $"wanted=({wanted.Rps},{wanted.Burst}) " +
                $"closest=({closest.Rps},{closest.Burst})");

            return reused;
        }

        return null;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private Policy FIND_CLOSEST_POLICY(Policy wanted)
    {
        Policy closest = default;
        System.Int32 bestDistance = System.Int32.MaxValue;
        System.Boolean found = false;

        foreach (Policy candidate in _limiters.Keys)
        {
            System.Int32 distance = CALCULATE_POLICY_DISTANCE(candidate, wanted);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
                found = true;

                if (distance == 0)
                {
                    break;
                }
            }
        }

        return found ? closest : default;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 CALCULATE_POLICY_DISTANCE(Policy a, Policy b)
        => System.Math.Abs(a.Rps - b.Rps) + System.Math.Abs(a.Burst - b.Burst);

    private Entry CREATE_NEW_LIMITER_ENTRY(Policy policy)
    {
        TokenBucketOptions options = CREATE_OPTIONS_FOR_POLICY(policy);
        Entry newEntry = new(new TokenBucketLimiter(options));

        Entry actualEntry = _limiters.GetOrAdd(policy, newEntry);

        if (ReferenceEquals(actualEntry, newEntry))
        {
            s_logger?.Info(
                $"[NW.{nameof(PolicyRateLimiter)}] created-policy-limiter " +
                $"rps={policy.Rps} burst={policy.Burst} total={_limiters.Count}");
        }
        else
        {
            newEntry.Dispose();
        }

        actualEntry.Touch();
        return actualEntry;
    }

    #endregion Rate Limit Check

    #region Decision Helpers

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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void TRY_SCHEDULE_SWEEP()
    {
        System.Int32 count = System.Threading.Interlocked.Increment(ref _checkCounter);

        if (count < 0)
        {
            System.Threading.Interlocked.CompareExchange(ref _checkCounter, 1, count);
            count = 1;
        }

        if ((count & (SweepEveryNChecks - 1)) == 0)
        {
            _ = System.Threading.Tasks.Task.Run(EVICT_STALE_POLICIES);
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void EVICT_STALE_POLICIES()
    {
        System.Int64 nowTicks = System.DateTime.UtcNow.Ticks;
        System.Int32 evictedCount = 0;

        foreach (var (policy, entry) in _limiters)
        {
            if (entry.IsStale(nowTicks, PolicyTtlSeconds))
            {
                if (_limiters.TryRemove(policy, out Entry removed))
                {
                    removed.Dispose();
                    evictedCount++;
                }
            }
        }

        if (evictedCount > 0)
        {
            s_logger?.Debug(
                $"[NW.{nameof(PolicyRateLimiter)}] evicted-stale-policies " +
                $"count={evictedCount} remaining={_limiters.Count}");
        }
    }

    #endregion Cleanup
}