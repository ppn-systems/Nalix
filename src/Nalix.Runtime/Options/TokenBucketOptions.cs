// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Provides configuration options for a high-performance token-bucket rate limiter.
/// </summary>
[IniComment("Token-bucket rate limiter configuration — controls burst capacity, refill rate, sharding, and violation policy")]
public sealed class TokenBucketOptions : ConfigurationLoader
{
    #region Properties

    /// <summary>
    /// Gets or sets the maximum number of tokens (bucket capacity).
    /// </summary>
    [IniComment("Maximum burst size in tokens — determines how many requests can fire at once (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "CapacityTokens must be positive")]
    public int CapacityTokens { get; set; } = 12;

    /// <summary>
    /// Gets or sets the refill rate in tokens per second.
    /// </summary>
    [IniComment("Sustained throughput rate in tokens per second (typically CapacityTokens / window)")]
    [System.ComponentModel.DataAnnotations.Range(0.001, double.MaxValue, ErrorMessage = "RefillTokensPerSecond must be positive")]
    public double RefillTokensPerSecond { get; set; } = 6.0;

    /// <summary>
    /// Gets or sets the hard lockout duration in seconds after a throttle decision.
    /// </summary>
    [IniComment("Hard lockout duration in seconds after throttling (0 = disabled)")]
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue, ErrorMessage = "HardLockoutSeconds cannot be negative")]
    public int HardLockoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the duration in seconds after which an idle endpoint entry is considered stale.
    /// </summary>
    [IniComment("Seconds before an idle endpoint entry is eligible for cleanup (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "StaleEntrySeconds must be positive")]
    public int StaleEntrySeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the cleanup interval in seconds.
    /// </summary>
    [IniComment("How often stale endpoint entries are purged in seconds (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "CleanupIntervalSeconds must be positive")]
    public int CleanupIntervalSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the fixed-point resolution for token arithmetic (micro-tokens per token).
    /// </summary>
    [IniComment("Fixed-point precision for token arithmetic (1–1,000,000; higher = more precise)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000, ErrorMessage = "TokenScale must be positive")]
    public int TokenScale { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the number of shards for endpoint partitioning.
    /// </summary>
    [IniComment("Shard count for endpoint partitioning — must be a power of two (e.g. 16, 32, 64)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "ShardCount must be positive")]
    public int ShardCount { get; set; } = 32;

    /// <summary>
    /// Gets or sets the time window in seconds for tracking soft rate limit violations.
    /// </summary>
    [IniComment("Window in seconds for counting soft violations before escalation (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "SoftViolationWindowSeconds must be positive")]
    public int SoftViolationWindowSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of soft violations allowed within the soft violation window.
    /// </summary>
    [IniComment("Max soft violations within SoftViolationWindowSeconds before stricter penalties apply (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "MaxSoftViolations must be positive")]
    public int MaxSoftViolations { get; set; } = 3;

    /// <summary>
    /// Gets or sets the cooldown reset duration in seconds.
    /// </summary>
    [IniComment("Seconds before violation count or lockout state is reset after a penalty (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "CooldownResetSec must be positive")]
    public int CooldownResetSec { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of endpoints that can be tracked simultaneously.
    /// </summary>
    [IniComment("Max tracked endpoints to prevent unbounded memory growth (0 = unlimited, not recommended)")]
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue, ErrorMessage = "MaxTrackedEndpoints cannot be negative")]
    public int MaxTrackedEndpoints { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the initial number of tokens for new endpoints.
    /// </summary>
    [IniComment("Initial tokens for new endpoints (-1 = full capacity, 0 = empty/cold-start mode)")]
    public int InitialTokens { get; set; } = -1;

    /// <summary>
    /// Maximum capacity for the eviction queue to prevent spikes in cleanup latency.
    /// </summary>
    [IniComment("Max items processed per cleanup cycle to cap latency (default 4096)")]
    [System.ComponentModel.DataAnnotations.Range(64, 65536)]
    public int MaxEvictionCapacity { get; set; } = 4096;

    /// <summary>
    /// Minimum initial capacity for report list to avoid reallocations.
    /// </summary>
    [IniComment("Initial capacity for diagnostic report generation (default 256)")]
    [System.ComponentModel.DataAnnotations.Range(64, 8192)]
    public int MinReportCapacity { get; set; } = 256;

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

        if (this.ShardCount <= 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                "ShardCount must be positive and power-of-two.");
        }

        static bool IsPowerOfTwo(int x) => (x & (x - 1)) == 0;
        if (!IsPowerOfTwo(this.ShardCount))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("ShardCount must be a power of two (e.g., 16, 32, 64) to ensure correct shard distribution.");
        }

        if (this.CapacityTokens * (long)this.TokenScale > long.MaxValue)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("CapacityTokens * TokenScale is too large and may overflow Int64. Reduce values.");
        }
    }
}
