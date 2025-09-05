// Copyright (c) 2026 PPN Corporation. All rights reserved.

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
    #region Properties

    /// <summary>
    /// Gets or sets the maximum number of tokens (bucket capacity).
    /// </summary>
    /// <remarks>
    /// Typical values: 10–100.
    /// Determines the maximum burst size allowed.
    /// Default is 12.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "CapacityTokens must be positive")]
    public System.Int32 CapacityTokens { get; set; } = 12;

    /// <summary>
    /// Gets or sets the refill rate in tokens per second.
    /// </summary>
    /// <remarks>
    /// Typically set to <c>CapacityTokens / windowSeconds</c>.
    /// Controls the sustained throughput rate.
    /// Default is 6.0 tokens per second.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(0.001, System.Double.MaxValue, ErrorMessage = "RefillTokensPerSecond must be positive")]
    public System.Double RefillTokensPerSecond { get; set; } = 6.0;

    /// <summary>
    /// Gets or sets the hard lockout duration in seconds after a throttle decision.
    /// </summary>
    /// <remarks>
    /// If set to 0, no hard lockout is applied (only soft backoff via Retry-After).
    /// Use this to enforce stricter penalties on abusive clients.
    /// Default is 0 (disabled).
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue, ErrorMessage = "HardLockoutSeconds cannot be negative")]
    public System.Int32 HardLockoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the duration in seconds after which an idle endpoint entry
    /// is considered stale and removed by cleanup.
    /// </summary>
    /// <remarks>
    /// Default is 300 seconds (5 minutes).
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "StaleEntrySeconds must be positive")]
    public System.Int32 StaleEntrySeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the cleanup interval in seconds.
    /// </summary>
    /// <remarks>
    /// Default is 60 seconds (1 minute).
    /// Controls how often stale entries are removed.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "CleanupIntervalSeconds must be positive")]
    public System.Int32 CleanupIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the fixed-point resolution for token arithmetic
    /// (micro-tokens per token).
    /// </summary>
    /// <remarks>
    /// Default is 1,000.
    /// Higher values improve precision but may add overhead.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "TokenScale must be positive")]
    public System.Int32 TokenScale { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the number of shards for endpoint partitioning.
    /// </summary>
    /// <remarks>
    /// A power-of-two value is recommended (e.g., 64).
    /// Sharding reduces contention on hot paths by distributing state.
    /// Default is 32.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "ShardCount must be positive")]
    public System.Int32 ShardCount { get; set; } = 32;

    /// <summary>
    /// Gets or sets the time window in seconds for tracking soft rate limit violations.
    /// </summary>
    /// <remarks>
    /// Determines the period during which soft violations are counted before escalation.
    /// Default is 5 seconds.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "SoftViolationWindowSeconds must be positive")]
    public System.Int32 SoftViolationWindowSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of soft violations allowed within the soft violation window before escalation.
    /// </summary>
    /// <remarks>
    /// If the number of soft violations within <see cref="SoftViolationWindowSeconds"/> exceeds this value,
    /// stricter rate limiting or penalties may be applied.
    /// Default is 3.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "MaxSoftViolations must be positive")]
    public System.Int32 MaxSoftViolations { get; set; } = 3;

    /// <summary>
    /// Gets or sets the cooldown reset duration in seconds.
    /// </summary>
    /// <remarks>
    /// After a hard or soft violation, this value determines how long before the violation count or lockout state is reset.
    /// Default is 10 seconds.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "CooldownResetSec must be positive")]
    public System.Int32 CooldownResetSec { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of endpoints that can be tracked simultaneously.
    /// </summary>
    /// <remarks>
    /// This limit prevents unbounded memory growth and DoS attacks via endpoint exhaustion.
    /// When the limit is reached, the oldest stale entries are evicted.
    /// A value of 0 means no limit (not recommended for production).
    /// Default is 10,000 endpoints.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue, ErrorMessage = "MaxTrackedEndpoints cannot be negative")]
    public System.Int32 MaxTrackedEndpoints { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the initial number of tokens for new endpoints.
    /// Default is -1 (start with full capacity).
    /// Set to 0 to start empty (cold-start mode for aggressive rate limiting).
    /// </summary>
    public System.Int32 InitialTokens { get; set; } = -1;

    #endregion Properties

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
