// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Security.Hashing;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Throttling;

/// <summary>
/// Provides a policy-based rate limiting mechanism using a shared stateless token bucket engine.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PolicyRateLimiter"/> extracts rate limit policies (RPS and burst capacity)
/// directly from packet attributes and evaluates them against a single, highly-optimized 
/// <see cref="TokenBucketLimiter"/> backend.
/// </para>
/// <para>
/// By utilizing the dynamic <see cref="TokenBucketLimiter.RateLimitPolicy"/> API, this class
/// completely eliminates the need for policy caching, quantization, and background eviction 
/// threads, resulting in near-zero allocation and bounded memory footprint regardless of 
/// how many distinct policies are defined by the user.
/// </para>
/// <para>
/// This class is thread-safe and optimized for high-throughput network environments.
/// </para>
/// </remarks>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed class PolicyRateLimiter : IReportable, IDisposable, IWithLogging<PolicyRateLimiter>
{
    #region Fields

    private int _disposed;
    private int _checkCounter;

    // The single, shared engine that handles ALL policies statelessly
    private readonly TokenBucketLimiter _shared;
    private readonly ConcurrentDictionary<(int rps, double burst, int hardLockout, int maxVio), TokenBucketLimiter.RateLimitPolicy> _policyCache;

    /// <summary>
    /// Gets the default rate limiting options used for global configuration (shard count, cleanups).
    /// </summary>
    private static readonly TokenBucketOptions s_defaults = ConfigurationManager.Instance.Get<TokenBucketOptions>();

    private ILogger? _logger;

    #endregion Fields

    #region Private Types

    /// <summary>
    /// A composite endpoint key that isolates token buckets by IP Address, and either Operation Code or a specific Policy ID.
    /// </summary>
    private readonly struct ScopedEndpoint : INetworkEndpoint, IEquatable<ScopedEndpoint>
    {
        private readonly ushort _op;
        private readonly string _ip;
        private readonly string? _policyId;
        private readonly INetworkEndpoint _inner;

        public ScopedEndpoint(ushort op, string? policyId, INetworkEndpoint inner)
        {
            _op = op;
            _ip = inner.Address;
            _policyId = policyId;
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string Address => _inner.Address;
        public bool HasPort => _inner.HasPort;
        public bool IsIPv6 => _inner.IsIPv6;
        public int Port => _inner.Port;

        public override int GetHashCode() => ComputeHash(_op, _policyId, _ip);

        public bool Equals(ScopedEndpoint other) =>
            _op == other._op &&
            string.Equals(_policyId, other._policyId, StringComparison.Ordinal) &&
            string.Equals(_ip, other._ip, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ScopedEndpoint other && this.Equals(other);

        public static bool operator ==(ScopedEndpoint left, ScopedEndpoint right) => left.Equals(right);

        public static bool operator !=(ScopedEndpoint left, ScopedEndpoint right) => !left.Equals(right);

        public override string ToString() => $"op:{_op:X4}|policy:{_policyId ?? "null"}|ip:{_ip}";

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static int ComputeHash(ushort op, string? policyId, string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                return op; // fallback
            }

            ReadOnlySpan<byte> ipBytes = MemoryMarshal.AsBytes(ip.AsSpan());

            // OPTIMIZATION: If PolicyId is present, use its hash as the XxHash32 seed to isolate the bucket.
            // Otherwise, fallback to the operation code as the seed.
            uint seed = policyId != null ? (uint)policyId.GetHashCode(StringComparison.Ordinal) : op;

            uint hash = XxHash32.Compute(ipBytes, seed: seed);

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    #endregion Private Types

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyRateLimiter"/> class.
    /// </summary>
    /// <remarks>
    /// Default rate limiting options are loaded from configuration at startup to initialize the shared engine.
    /// </remarks>
    public PolicyRateLimiter()
    {
        _disposed = 0;
        _checkCounter = 0;

        // Instantiate the single shared engine that will process all dynamic policies
        _policyCache = new();
        _shared = InstanceManager.Instance.GetOrCreateInstance<TokenBucketLimiter>();
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
        _ = _shared.WithLogging(_logger);
        return this;
    }

    /// <summary>
    /// Performs a rate limit check for the specified operation code and packet context.
    /// </summary>
    /// <param name="opCode">The operation code associated with the incoming packet.</param>
    /// <param name="context">The packet context containing connection, endpoint, and rate limit metadata.</param>
    /// <returns>
    /// A <see cref="TokenBucketLimiter.RateLimitDecision"/> indicating whether the request
    /// is allowed, throttled, or denied.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the limiter has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <c>null</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TokenBucketLimiter.RateLimitDecision Evaluate(ushort opCode, IPacketContext<IPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        PacketRateLimitAttribute? rl = context.Attributes.RateLimit;

        // Fast-path: No limits applied or invalid configuration
        if (rl is null || rl.RequestsPerSecond <= 0)
        {
            return CREATE_ALLOWED_DECISION();
        }

        // Fast-path: Invalid burst
        if (rl.Burst <= 0)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"[RT.{nameof(PolicyRateLimiter)}] invalid-burst burst={rl.Burst}");
            }
            return CREATE_DENIED_DECISION(isHard: true);
        }

        TokenBucketLimiter.RateLimitPolicy dynamicPolicy = this.EXTRACT_DYNAMIC_POLICY(rl);

        return this.PERFORM_RATE_LIMIT_CHECK(opCode, rl.PolicyId, context, in dynamicPolicy);
    }

    /// <summary>
    /// Generates a human-readable diagnostic report describing the current rate limiter state.
    /// </summary>
    public string GenerateReport()
    {
        StringBuilder sb = new();

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PolicyRateLimiter Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Evaluate Counter      : {Volatile.Read(ref _checkCounter):N0}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("--- Shared TokenBucket Engine Report ---");
        _ = sb.Append(_shared.GenerateReport());

        return sb.ToString();
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber("CheckCounter", Volatile.Read(ref _checkCounter));

        writer.WritePropertyName("SharedEngine");
        _shared.WriteReportData(writer);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="PolicyRateLimiter"/>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _shared.Dispose();

        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation($"[RT.{nameof(PolicyRateLimiter)}:{nameof(Dispose)}] disposed");
        }

        GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Policy Management

    /// <summary>
    /// Creates a dynamic rate limit policy structurally compatible with the token bucket engine.
    /// Quantization is no longer required due to stateless evaluation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TokenBucketLimiter.RateLimitPolicy EXTRACT_DYNAMIC_POLICY(PacketRateLimitAttribute rl)
    {
        // 1. Normalize the parameters
        int rps = rl.RequestsPerSecond;
        double burst = Math.Max(1.0, rl.Burst); // Ensure CapacityTokens >= 1
        int hardLockout = rl.HardLockoutSeconds > 0 ? rl.HardLockoutSeconds : s_defaults.HardLockoutSeconds;
        int maxViolations = rl.MaxSoftViolations > 0 ? rl.MaxSoftViolations : s_defaults.MaxSoftViolations;

        // 2. Create ValueTuple Key (Completely on register/stack, no garbage)
        (int rps, double burst, int hardLockout, int maxViolations) cacheKey = (rps, burst, hardLockout, maxViolations);

        // 3. Check in Cache (Fast-path)
        if (_policyCache.TryGetValue(cacheKey, out TokenBucketLimiter.RateLimitPolicy cachedPolicy))
        {
            return cachedPolicy;
        }

        // 4. If not present, calculate for the first time (Slow-path)
        TokenBucketLimiter.RateLimitPolicy newPolicy = new(
            rps, burst,
            s_defaults.TokenScale, Stopwatch.Frequency,
            hardLockout,
            maxViolations
        );

        // 5. Store in cache for subsequent requests
        _ = _policyCache.TryAdd(cacheKey, newPolicy);

        return newPolicy;
    }

    #endregion Policy Management

    #region Rate Limit Check

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TokenBucketLimiter.RateLimitDecision PERFORM_RATE_LIMIT_CHECK(ushort opCode, string? policyId, IPacketContext<IPacket> context, in TokenBucketLimiter.RateLimitPolicy policy)
    {
        if (context.Connection?.NetworkEndpoint is null)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"[RT.{nameof(PolicyRateLimiter)}] missing-endpoint opCode={opCode}");
            }

            return CREATE_DENIED_DECISION(isHard: false);
        }

        // Isolate keyspace by hashing PolicyId (if present) or OpCode, and IP Address
        ScopedEndpoint subject = new(opCode, policyId, context.Connection.NetworkEndpoint);

        _ = Interlocked.Increment(ref _checkCounter);

        // Feed the subject and the dynamic policy directly into the shared engine
        return _shared.Evaluate(subject, in policy);
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
    private static TokenBucketLimiter.RateLimitDecision CREATE_DENIED_DECISION(bool isHard, int retryAfterMs = 0)
    {
        return new TokenBucketLimiter.RateLimitDecision
        {
            Credit = 0,
            Allowed = false,
            RetryAfterMs = retryAfterMs > 0 ? retryAfterMs : (isHard ? int.MaxValue : 1000),
            Reason = isHard ? TokenBucketLimiter.RateLimitReason.HardLockout : TokenBucketLimiter.RateLimitReason.SoftThrottle
        };
    }

    #endregion Decision Helpers
}
