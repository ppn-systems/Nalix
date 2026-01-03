// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Provides configuration options for a high-performance token-bucket rate limiter.
/// </summary>
/// <remarks>
/// This limiter is used to control request rates per endpoint or client, based on
/// the classic token-bucket algorithm.
/// Legacy <c>RateLimitOptions</c> can be mapped into this configuration.
/// </remarks>
public sealed class TokenBucketOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of tokens (bucket capacity).
    /// </summary>
    /// <remarks>
    /// Typical values: 10–100.
    /// Determines the maximum burst size allowed.
    /// Default is 12.
    /// </remarks>
    public System.Int32 CapacityTokens { get; set; } = 12;

    /// <summary>
    /// Gets or sets the refill rate in tokens per second.
    /// </summary>
    /// <remarks>
    /// Typically set to <c>CapacityTokens / windowSeconds</c>.
    /// Controls the sustained throughput rate.
    /// Default is 6.0 tokens per second.
    /// </remarks>
    public System.Double RefillTokensPerSecond { get; set; } = 6.0;

    /// <summary>
    /// Gets or sets the hard lockout duration in seconds after a throttle decision.
    /// </summary>
    /// <remarks>
    /// If set to 0, no hard lockout is applied (only soft backoff via Retry-After).
    /// Use this to enforce stricter penalties on abusive clients.
    /// Default is 0 (disabled).
    /// </remarks>
    public System.Int32 HardLockoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the duration in seconds after which an idle endpoint entry
    /// is considered stale and removed by cleanup.
    /// </summary>
    /// <remarks>
    /// Default is 300 seconds (5 minutes).
    /// </remarks>
    public System.Int32 StaleEntrySeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the cleanup interval in seconds.
    /// </summary>
    /// <remarks>
    /// Default is 60 seconds (1 minute).
    /// Controls how often stale entries are removed.
    /// </remarks>
    public System.Int32 CleanupIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the fixed-point resolution for token arithmetic
    /// (micro-tokens per token).
    /// </summary>
    /// <remarks>
    /// Default is 1,000.
    /// Higher values improve precision but may add overhead.
    /// </remarks>
    public System.Int32 TokenScale { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the number of shards for endpoint partitioning.
    /// </summary>
    /// <remarks>
    /// A power-of-two value is recommended (e.g., 64).
    /// Sharding reduces contention on hot paths by distributing state.
    /// Default is 32.
    /// </remarks>
    public System.Int32 ShardCount { get; set; } = 32;

    /// <summary>
    /// Gets or sets the time window in seconds for tracking soft rate limit violations.
    /// </summary>
    /// <remarks>
    /// Determines the period during which soft violations are counted before escalation.
    /// Default is 5 seconds.
    /// </remarks>
    public System.Int32 SoftViolationWindowSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of soft violations allowed within the soft violation window before escalation.
    /// </summary>
    /// <remarks>
    /// If the number of soft violations within <see cref="SoftViolationWindowSeconds"/> exceeds this value,
    /// stricter rate limiting or penalties may be applied.
    /// Default is 3.
    /// </remarks>
    public System.Int32 MaxSoftViolations { get; set; } = 3;

    /// <summary>
    /// Gets or sets the cooldown reset duration in seconds.
    /// </summary>
    /// <remarks>
    /// After a hard or soft violation, this value determines how long before the violation count or lockout state is reset.
    /// Default is 10 seconds.
    /// </remarks>
    public System.Int32 CooldownResetSec { get; set; } = 10;
}
