// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Network.Pipeline.Options;

namespace Nalix.Network.Pipeline.Throttling;

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
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed class PolicyRateLimiter : IReportable, IDisposable, IWithLogging<PolicyRateLimiter>
{
    #region Constants

    private const int MaxPolicies = 64;
    private const int PolicyTtlSeconds = 1800;
    private const int SweepEveryNChecks = 1024;

    #endregion Constants

    #region Fields

    private int _checkCounter;
    private int _disposed;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Policy, Entry> _limiters = new();

    private static readonly int[] s_rpsTiers = [1, 2, 4, 8, 16, 32, 64, 128];
    private static readonly double[] s_burstTiers = [0.1, 0.2, 0.5, 1, 2, 4, 8, 16, 32, 64];

    private static readonly TokenBucketOptions s_defaults = ConfigurationManager.Instance.Get<TokenBucketOptions>();
    private ILogger? _logger;

    #endregion Fields

    #region Private Types

    private sealed class Entry : IDisposable
    {
        private readonly ILogger? _logger;
        private long _lastUsedUtcTicks;
        private int _activeUsers;
        private int _disposed;
        private readonly ManualResetEventSlim _idleSignal = new(true);

        public TokenBucketLimiter Limiter { get; }

        public long LastUsedUtcTicks =>
            Interlocked.Read(ref _lastUsedUtcTicks);

        public Entry(TokenBucketLimiter limiter, ILogger? logger = null)
        {
            this.Limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
            _logger = logger;
            _activeUsers = 0;
            _disposed = 0;
            this.Touch();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            _ = Interlocked.Exchange(
                ref _lastUsedUtcTicks,
                DateTime.UtcNow.Ticks);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAcquire()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            int newCount = Interlocked.Increment(ref _activeUsers);
            _idleSignal.Reset();

            if (Volatile.Read(ref _disposed) != 0)
            {
                _ = Interlocked.Decrement(ref _activeUsers);
                if (Volatile.Read(ref _activeUsers) == 0)
                {
                    _idleSignal.Set();
                }
                return false;
            }

            if (newCount <= 0)
            {
                _ = Interlocked.Decrement(ref _activeUsers);
                _logger?.Error($"[NW.{nameof(PolicyRateLimiter)}:Entry] active-users-overflow");
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            int remaining = Interlocked.Decrement(ref _activeUsers);

            if (remaining < 0)
            {
                _logger?.Error($"[NW.{nameof(PolicyRateLimiter)}:Entry] active-users-overflow");
                _ = Interlocked.Exchange(ref _activeUsers, 0);
                _idleSignal.Set();
                return;
            }

            if (remaining == 0)
            {
                _idleSignal.Set();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStale(long nowTicks, int ttlSeconds)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return true;
            }

            long lastTicks = Interlocked.Read(ref _lastUsedUtcTicks);
            long ttlTicks = ttlSeconds * TimeSpan.TicksPerSecond;
            return nowTicks - lastTicks > ttlTicks;
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            const int maxWaitMs = 500;
            _ = _idleSignal.Wait(maxWaitMs);

            int remainingUsers = Volatile.Read(ref _activeUsers);

            try
            {
                this.Limiter.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Error($"[NW.{nameof(PolicyRateLimiter)}:Entry] disposal-error", ex);
            }

            _idleSignal.Dispose();
        }
    }

    private readonly record struct Policy(int Rps, double Burst);

    private readonly struct RateLimitSubject(ushort op, INetworkEndpoint inner) : INetworkEndpoint, IEquatable<RateLimitSubject>
    {
        private readonly ushort _op = op;
        private readonly INetworkEndpoint _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        // Hot-path: avoid per-evaluation string allocation.
        public string Address => _inner.Address;

        public override int GetHashCode() => HashCode.Combine(_op, _inner);

        public bool Equals(RateLimitSubject other) => _op == other._op && _inner.Equals(other._inner);

        public override bool Equals(object? obj) => obj is RateLimitSubject other && this.Equals(other);

        public static bool operator ==(RateLimitSubject left, RateLimitSubject right)
            => left.Equals(right);

        public static bool operator !=(RateLimitSubject left, RateLimitSubject right)
            => !left.Equals(right);

        public override string ToString() => $"op:{_op:X4}|ep:{_inner.Address}";
    }

    private readonly struct CheckResult
    {
        public TokenBucketLimiter.RateLimitDecision Decision { get; init; }
        public bool Success { get; init; }
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
        _disposed = 0;
        _checkCounter = 0;
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Assigns a logger instance used by the limiter for diagnostic output.
    /// </summary>
    /// <param name="logger">The logger to use for subsequent diagnostics.</param>
    /// <returns>The current <see cref="PolicyRateLimiter"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PolicyRateLimiter WithLogging(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

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
    /// <exception cref="ObjectDisposedException">Thrown when the limiter has been disposed.</exception>
    /// <exception cref="ArgumentNullException">
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
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TokenBucketLimiter.RateLimitDecision Evaluate(ushort opCode, IPacketContext<IPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        CheckResult validationResult = this.VALIDATE_RATE_LIMIT_ATTRIBUTE(context);
        if (!validationResult.Success)
        {
            return validationResult.Decision;
        }

        Policy policy = EXTRACT_AND_QUANTIZE_POLICY(context.Attributes.RateLimit!);

        CheckResult checkResult = this.PERFORM_RATE_LIMIT_CHECK(opCode, context, policy);

        this.TRY_SCHEDULE_SWEEP();

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
    public string GenerateReport()
    {
        StringBuilder sb = new();

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PolicyRateLimiter Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Active Policies    : {_limiters.Count}/{MaxPolicies}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Evaluate Counter      : {Volatile.Read(ref _checkCounter):N0}");
        _ = sb.AppendLine();

        if (_limiters.IsEmpty)
        {
            _ = sb.AppendLine("(no active policies)");
            return sb.ToString();
        }

        _ = sb.AppendLine("Active Policies:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("RPS  | Burst | Last Used (UTC)");
        _ = sb.AppendLine("------------------------------------------------------------");

        List<KeyValuePair<Policy, Entry>> sorted = [.. _limiters.OrderByDescending(kv => kv.Key.Rps).ThenByDescending(kv => kv.Key.Burst)];

        foreach ((Policy policy, Entry entry) in sorted)
        {
            long lastTicks = entry.LastUsedUtcTicks;
            DateTime lastUsed = new(lastTicks, DateTimeKind.Utc);

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{policy.Rps,4} | {policy.Burst,5} | {lastUsed:yyyy-MM-dd HH:mm:ss}");
        }

        _ = sb.AppendLine("------------------------------------------------------------");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic summary of the policy rate limiter and all active policies.
    /// </summary>
    public IDictionary<string, object> GenerateReportData()
    {
        Dictionary<string, object> data = new()
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["ActivePolicies"] = _limiters.Count,
            ["MaxPolicies"] = MaxPolicies,
            ["CheckCounter"] = Volatile.Read(ref _checkCounter)
        };

        List<Dictionary<string, object>> active = [.. _limiters
            .OrderByDescending(x => x.Key.Rps)
            .ThenByDescending(x => x.Key.Burst)
            .Take(32)
            .Select(kv =>
            {
                Policy policy = kv.Key;
                Entry entry = kv.Value;
                DateTime lastUsed = new(entry.LastUsedUtcTicks, DateTimeKind.Utc);

                return new Dictionary<string, object>
                {
                    ["RPS"] = policy.Rps,
                    ["Burst"] = policy.Burst,
                    ["LastUsedUtc"] = lastUsed
                };
            })];

        data["Policies"] = active;

        return data;
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
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        int disposedCount = 0;
        int totalCount = _limiters.Count;
        using ManualResetEventSlim drainSignal = new(true);

        const int maxAttempts = 10;
        int attempt = 0;

        while (!_limiters.IsEmpty && attempt++ < maxAttempts)
        {
            foreach ((Policy policy, _) in _limiters)
            {
                if (_limiters.TryRemove(policy, out Entry? removed) && removed is not null)
                {
                    try
                    {
                        removed.Dispose();
                        disposedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error(
                            $"[NW.{nameof(PolicyRateLimiter)}:{nameof(Dispose)}] " +
                            $"disposal-error policy={policy}", ex);
                    }
                }
            }

            if (!_limiters.IsEmpty)
            {
                _ = drainSignal.Wait(50);
            }
        }

        if (!_limiters.IsEmpty)
        {
            _limiters.Clear();
        }

        _logger?.Info($"[NW.{nameof(PolicyRateLimiter)}:{nameof(Dispose)}] disposed={disposedCount}/{totalCount}");

        GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Validation

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CheckResult VALIDATE_RATE_LIMIT_ATTRIBUTE(IPacketContext<IPacket> context)
    {
        PacketRateLimitAttribute? rl = context.Attributes.RateLimit;

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
            _logger?.Warn($"[NW.{nameof(PolicyRateLimiter)}] invalid-burst burst={rl.Burst}");

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Policy EXTRACT_AND_QUANTIZE_POLICY(PacketRateLimitAttribute rl)
    {
        int rps = QUANTIZE_VALUE(rl.RequestsPerSecond, s_rpsTiers);
        double burst = QUANTIZE_VALUE(rl.Burst, s_burstTiers);

        return new Policy(rps, burst);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int QUANTIZE_VALUE(int value, int[] tiers)
    {
        foreach (int tier in tiers)
        {
            if (value <= tier)
            {
                return tier;
            }
        }

        return tiers[^1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double QUANTIZE_VALUE(double value, double[] tiers)
    {
        foreach (double tier in tiers)
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
            CapacityTokens = (int)policy.Burst,
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

    private CheckResult PERFORM_RATE_LIMIT_CHECK(ushort opCode, IPacketContext<IPacket> context, Policy policy)
    {
        if (context.Connection?.NetworkEndpoint is null)
        {
            _logger?.Warn($"[NW.{nameof(PolicyRateLimiter)}] missing-endpoint opCode={opCode}");

            return new CheckResult
            {
                Success = false,
                Decision = CREATE_DENIED_DECISION(isHard: false)
            };
        }

        Entry entry = this.GET_OR_CREATE_LIMITER_ENTRY(policy);

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
            RateLimitSubject subject = new(opCode, context.Connection.NetworkEndpoint);
            TokenBucketLimiter.RateLimitDecision decision = entry.Limiter.Evaluate(subject);

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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Entry GET_OR_CREATE_LIMITER_ENTRY(Policy policy)
    {
        if (_limiters.TryGetValue(policy, out Entry? existingEntry) && existingEntry is not null)
        {
            existingEntry.Touch();
            return existingEntry;
        }

        if (this.IS_AT_POLICY_CAPACITY())
        {
            Entry? reusedEntry = this.TRY_REUSE_CLOSEST_POLICY(policy);
            if (reusedEntry is not null)
            {
                return reusedEntry;
            }
        }

        return this.CREATE_NEW_LIMITER_ENTRY(policy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IS_AT_POLICY_CAPACITY() => _limiters.Count >= MaxPolicies;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Entry? TRY_REUSE_CLOSEST_POLICY(Policy wanted)
    {
        if (_limiters.IsEmpty)
        {
            return null;
        }

        Policy closest = this.FIND_CLOSEST_POLICY(wanted);

        if (closest.Equals(default))
        {
            return null;
        }

        if (_limiters.TryGetValue(closest, out Entry? reused) && reused is not null)
        {
            reused.Touch();

            _logger?.Debug($"[NW.{nameof(PolicyRateLimiter)}] reusing-policy wanted=({wanted.Rps},{wanted.Burst}) closest=({closest.Rps},{closest.Burst})");

            return reused;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Policy FIND_CLOSEST_POLICY(Policy wanted)
    {
        Policy closest = default;
        int bestDistance = int.MaxValue;
        bool found = false;

        foreach (Policy candidate in _limiters.Keys)
        {
            int distance = CALCULATE_POLICY_DISTANCE(candidate, wanted);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CALCULATE_POLICY_DISTANCE(Policy a, Policy b) => (int)(Math.Abs(a.Rps - b.Rps) + Math.Abs(a.Burst - b.Burst));

    private Entry CREATE_NEW_LIMITER_ENTRY(Policy policy)
    {
        TokenBucketOptions options = CREATE_OPTIONS_FOR_POLICY(policy);
        TokenBucketLimiter limiter = new(options);
        if (_logger is not null)
        {
            _ = limiter.WithLogging(_logger);
        }

        Entry newEntry = new(limiter, _logger);

        Entry actualEntry = _limiters.GetOrAdd(policy, newEntry);

        if (ReferenceEquals(actualEntry, newEntry))
        {
            _logger?.Info($"[NW.{nameof(PolicyRateLimiter)}] created-policy-limiter rps={policy.Rps} burst={policy.Burst} total={_limiters.Count}");
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TokenBucketLimiter.RateLimitDecision CREATE_ALLOWED_DECISION()
    {
        return new TokenBucketLimiter.RateLimitDecision
        {
            Allowed = true,
            RetryAfterMs = 0,
            Credit = ushort.MaxValue,
            Reason = TokenBucketLimiter.RateLimitReason.None
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TokenBucketLimiter.RateLimitDecision CREATE_DENIED_DECISION(
        bool isHard,
        int retryAfterMs = 0)
    {
        return new TokenBucketLimiter.RateLimitDecision
        {
            Allowed = false,
            RetryAfterMs = retryAfterMs > 0 ? retryAfterMs : (isHard ? int.MaxValue : 1000),
            Credit = 0,
            Reason = isHard
                ? TokenBucketLimiter.RateLimitReason.HardLockout
                : TokenBucketLimiter.RateLimitReason.SoftThrottle
        };
    }

    #endregion Decision Helpers

    #region Cleanup

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TRY_SCHEDULE_SWEEP()
    {
        int count = Interlocked.Increment(ref _checkCounter);

        if (count < 0)
        {
            _ = Interlocked.CompareExchange(ref _checkCounter, 1, count);
            count = 1;
        }

        if ((count & (SweepEveryNChecks - 1)) == 0)
        {
            _ = ThreadPool.UnsafeQueueUserWorkItem(
                static state => state.EVICT_STALE_POLICIES(),
                this,
                preferLocal: false);
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EVICT_STALE_POLICIES()
    {
        long nowTicks = DateTime.UtcNow.Ticks;
        int evictedCount = 0;

        foreach ((Policy policy, Entry entry) in _limiters)
        {
            if (entry.IsStale(nowTicks, PolicyTtlSeconds) && _limiters.TryRemove(policy, out Entry? removed) && removed is not null)
            {
                removed.Dispose();
                evictedCount++;
            }
        }

        if (evictedCount > 0)
        {
            _logger?.Debug(
                $"[NW.{nameof(PolicyRateLimiter)}] evicted-stale-policies " +
                $"count={evictedCount} remaining={_limiters.Count}");
        }
    }

    #endregion Cleanup
}
