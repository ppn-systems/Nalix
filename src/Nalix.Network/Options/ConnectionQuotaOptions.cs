// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration options that control connection limiting
/// behavior per IP address.
/// </summary>
[IniComment("Per-IP connection limiting — mitigates abuse, DoS, and excessive resource consumption")]
public sealed partial class ConnectionQuotaOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed per IP address.
    /// </summary>
    [IniComment("Max concurrent connections from a single IP address (1–10,000)")]
    [ValueRange(1, 10_000)]
    public int MaxConnectionsPerIpAddress { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of connection attempts allowed within the configured rate window.
    /// </summary>
    [IniComment("Max connection attempts from one IP within the rate window (1–10,000,000)")]
    [ValueRange(1, 10_000_000)]
    public int MaxConnectionsPerWindow { get; set; } = 10;

    /// <summary>
    /// Gets or sets the time window used to evaluate connection rate limits.
    /// </summary>
    [IniComment("Sliding window for counting connection attempts per IP (00:00:01–00:10:00)")]
    [DurationRange("00:00:01", "00:10:00")]
    public System.TimeSpan ConnectionRateWindow { get; set; } = System.TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the interval at which cleanup operations are performed.
    /// </summary>
    [IniComment("How often expired IP tracking entries are purged from memory (00:00:01–01:00:00)")]
    [DurationRange("00:00:01", "01:00:00")]
    public System.TimeSpan CleanupInterval { get; set; } = System.TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the duration after which an inactive connection is considered expired.
    /// </summary>
    [IniComment("Idle time before a connection is considered inactive and eligible for cleanup (00:00:01–1.00:00:00)")]
    [DurationRange("00:00:01", "1.00:00:00")]
    public System.TimeSpan InactivityThreshold { get; set; } = System.TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum number of expired entries to remove in a single cleanup cycle.
    /// If set to 0 (default), it will automatically scale to remove a percentage of the tracked entries.
    /// </summary>
    [IniComment("Max entries to clean per cycle. 0 = auto scale based on load (default 0)")]
    [ValueRange(0, 10_000_000)]
    public int MaxCleanupKeysPerRun { get; set; } = 0;

    /// <summary>
    /// Gets or sets the UTC offset to use when determining the start of a new day for connection limits.
    /// Default is TimeSpan.Zero (00:00 UTC).
    /// </summary>
    [IniComment("UTC offset for the daily connection limit reset. Example: 07:00:00 for GMT+7 (default 00:00:00)")]
    [DurationRange("-14:00:00", "14:00:00")]
    public System.TimeSpan DailyResetTimeOffset { get; set; } = System.TimeSpan.Zero;

    // === Subnet Protection ===

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed per /24 (IPv4) or /48 (IPv6) subnet.
    /// </summary>
    [IniComment("Max concurrent connections from a single /24 (IPv4) or /48 (IPv6) subnet (default 50)")]
    [ValueRange(1, 100_000)]
    public int MaxConnectionsPerSubnet { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of connection attempts allowed from a subnet within the rate window.
    /// </summary>
    [IniComment("Max connection attempts from a subnet within the rate window (default 100)")]
    [ValueRange(1, 10_000_000)]
    public int MaxSubnetConnectionsPerWindow { get; set; } = 100;

    // === Burst Detection ===

    /// <summary>
    /// Gets or sets the minimum interval between connections from the same IP to trigger burst mode.
    /// </summary>
    [IniComment("Minimum interval between connections from same IP in ms (default 50). Connections faster than this trigger burst mode.")]
    [ValueRange(0, 10_000)]
    public int MinConnectionIntervalMs { get; set; } = 50;

    /// <summary>
    /// Gets or sets the number of rapid connections needed to activate burst mode.
    /// </summary>
    [IniComment("Number of rapid connections needed to activate burst mode (default 3)")]
    [ValueRange(2, 100)]
    public int BurstThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the rate limit divisor applied during burst mode.
    /// </summary>
    [IniComment("Rate limit divisor applied during burst mode (default 2 = halve the limit)")]
    [ValueRange(1, 10)]
    public int BurstPenaltyDivisor { get; set; } = 2;

    // === Short-Lived Connection Detection ===

    /// <summary>
    /// Gets or sets the threshold for short-lived connections.
    /// </summary>
    [IniComment("Connections shorter than this (ms) count as short-lived and are penalized in rate window (default 2000)")]
    [ValueRange(0, 60_000)]
    public int ShortLivedThresholdMs { get; set; } = 2000;

    // === Adaptive Mode ===

    /// <summary>
    /// Gets or sets a value indicating whether adaptive tightening is enabled.
    /// </summary>
    [IniComment("Enable adaptive tightening when server load is high (default false)")]
    public bool EnableAdaptiveMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the load ratio threshold to trigger adaptive tightening.
    /// </summary>
    [IniComment("Load ratio threshold to trigger adaptive tightening (default 0.7 = 70%)")]
    [ValueRange(0.1, 0.99)]
    public double AdaptiveLoadThreshold { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the factor to multiply per-IP limit by when adaptive mode triggers.
    /// </summary>
    [IniComment("Factor to multiply per-IP limit by when adaptive mode triggers (default 0.5 = halve)")]
    [ValueRange(0.1, 1.0)]
    public double AdaptiveTighteningFactor { get; set; } = 0.5;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="Nalix.Abstractions.Validation.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate() => this.ValidateDataAnnotations();
}
