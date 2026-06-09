// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Provides configuration options for a high-performance token-bucket rate limiter.
/// </summary>
[IniComment("Token-bucket rate limiter configuration — controls burst capacity, refill rate, sharding, and violation policy")]
public sealed partial class TokenBucketOptions : ConfigurationLoader, IValidatableConfiguration
{
    #region Properties

    /// <summary>
    /// Gets or sets the maximum number of tokens (bucket capacity).
    /// </summary>
    [IniComment("Maximum burst size in tokens — determines how many requests can fire at once (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int CapacityTokens { get; set; } = 64;

    /// <summary>
    /// Gets or sets the refill rate in tokens per second.
    /// </summary>
    [IniComment("Sustained throughput rate in tokens per second (typically CapacityTokens / window)")]
    [ValueRange(0.001, double.MaxValue)]
    public double RefillTokensPerSecond { get; set; } = 32.0;

    /// <summary>
    /// Gets or sets the hard lockout duration in seconds after a throttle decision.
    /// </summary>
    [IniComment("Hard lockout duration in seconds after throttling (0 = disabled)")]
    [ValueRange(0, int.MaxValue)]
    public int HardLockoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the duration in seconds after which an idle endpoint entry is considered stale.
    /// </summary>
    [IniComment("Seconds before an idle endpoint entry is eligible for cleanup (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int StaleEntrySeconds { get; set; } = 180;

    /// <summary>
    /// Gets or sets the cleanup interval in seconds.
    /// </summary>
    [IniComment("How often stale endpoint entries are purged in seconds (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int CleanupIntervalSeconds { get; set; } = 45;

    /// <summary>
    /// Gets or sets the fixed-point resolution for token arithmetic (micro-tokens per token).
    /// </summary>
    [IniComment("Fixed-point precision for token arithmetic (1–1,000,000; higher = more precise)")]
    [ValueRange(1, 1_000_000)]
    public int TokenScale { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of shards for endpoint partitioning.
    /// </summary>
    [IniComment("Shard count for endpoint partitioning — must be a power of two (e.g. 16, 32, 64)")]
    [ValueRange(1, int.MaxValue)]
    public int ShardCount { get; set; } = 256;

    /// <summary>
    /// Gets or sets the time window in seconds for tracking soft rate limit violations.
    /// </summary>
    [IniComment("Window in seconds for counting soft violations before escalation (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int SoftViolationWindowSeconds { get; set; } = 8;

    /// <summary>
    /// Gets or sets the maximum number of soft violations allowed within the soft violation window.
    /// </summary>
    [IniComment("Max soft violations within SoftViolationWindowSeconds before stricter penalties apply (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int MaxSoftViolations { get; set; } = 5;

    /// <summary>
    /// Gets or sets the cooldown reset duration in seconds.
    /// </summary>
    [IniComment("Seconds before violation count or lockout state is reset after a penalty (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int CooldownResetSec { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of endpoints that can be tracked simultaneously.
    /// </summary>
    [IniComment("Max tracked endpoints to prevent unbounded memory growth (0 = unlimited, not recommended)")]
    [ValueRange(0, int.MaxValue)]
    public int MaxTrackedEndpoints { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the initial number of tokens for new endpoints.
    /// </summary>
    [IniComment("Initial tokens for new endpoints (-1 = full capacity, 0 = empty/cold-start mode)")]
    public int InitialTokens { get; set; } = -1;

    /// <summary>
    /// Maximum capacity for the eviction queue to prevent spikes in cleanup latency.
    /// </summary>
    [IniComment("Max items processed per cleanup cycle to cap latency (default 8192)")]
    [ValueRange(64, 65536)]
    public int MaxEvictionCapacity { get; set; } = 8192;

    /// <summary>
    /// Minimum initial capacity for report list to avoid reallocations.
    /// </summary>
    [IniComment("Initial capacity for diagnostic report generation (default 256)")]
    [ValueRange(64, 8192)]
    public int MinReportCapacity { get; set; } = 256;

    #endregion Properties

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="Nalix.Abstractions.Validation.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        this.ValidateDataAnnotations();

        if (this.ShardCount <= 0)
        {
            throw new Nalix.Abstractions.Validation.ValidationException(
                "ShardCount must be positive and power-of-two.");
        }

        static bool IsPowerOfTwo(int x) => (x & (x - 1)) == 0;
        if (!IsPowerOfTwo(this.ShardCount))
        {
            throw new Nalix.Abstractions.Validation.ValidationException("ShardCount must be a power of two (e.g., 16, 32, 64) to ensure correct shard distribution.");
        }

        if (this.CapacityTokens * (long)this.TokenScale > long.MaxValue)
        {
            throw new Nalix.Abstractions.Validation.ValidationException("CapacityTokens * TokenScale is too large and may overflow Int64. Reduce values.");
        }

        if (this.InitialTokens > this.CapacityTokens)
        {
            throw new Nalix.Abstractions.Validation.ValidationException(
                $"InitialTokens ({this.InitialTokens}) must be <= CapacityTokens ({this.CapacityTokens}).");
        }
    }
}
