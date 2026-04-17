// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration options that control connection limiting
/// behavior per IP address.
/// </summary>
[IniComment("Per-IP connection limiting — mitigates abuse, DoS, and excessive resource consumption")]
public sealed class ConnectionLimitOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed per IP address.
    /// </summary>
    [IniComment("Max concurrent connections from a single IP address (1–10,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000, ErrorMessage = "MaxConnectionsPerIpAddress must be between 1 and 10,000.")]
    public int MaxConnectionsPerIpAddress { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of connection attempts allowed within the configured rate window.
    /// </summary>
    [IniComment("Max connection attempts from one IP within the rate window (1–10,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "MaxConnectionsPerWindow must be between 1 and 10,000,000.")]
    public int MaxConnectionsPerWindow { get; set; } = 10;

    /// <summary>
    /// Gets or sets the ban duration for IPs that exceed the connection rate limit.
    /// </summary>
    [IniComment("How long an IP is banned after exceeding limits (00:00:01–1.00:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "1.00:00:00", ErrorMessage = "BanDuration must be at least 1 second and at most 1 day.")]
    public System.TimeSpan BanDuration { get; set; } = System.TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the time window used to evaluate connection rate limits.
    /// </summary>
    [IniComment("Sliding window for counting connection attempts per IP (00:00:01–00:10:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "00:10:00", ErrorMessage = "ConnectionRateWindow must be between 1 second and 10 minutes.")]
    public System.TimeSpan ConnectionRateWindow { get; set; } = System.TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the suppression window for DDoS-related log entries.
    /// </summary>
    [IniComment("Suppresses repeated DDoS log entries from the same IP within this window (00:00:01–01:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "DDoSLogSuppressWindow must be between 1 second and 1 hour.")]
    public System.TimeSpan DDoSLogSuppressWindow { get; set; } = System.TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets or sets the interval at which cleanup operations are performed.
    /// </summary>
    [IniComment("How often expired IP tracking entries are purged from memory (00:00:01–01:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "CleanupInterval must be between 1 second and 1 hour.")]
    public System.TimeSpan CleanupInterval { get; set; } = System.TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the duration after which an inactive connection is considered expired.
    /// </summary>
    [IniComment("Idle time before a connection is considered inactive and eligible for cleanup (00:00:01–1.00:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "1.00:00:00", ErrorMessage = "InactivityThreshold must be at least 1 second and at most 1 day.")]
    public System.TimeSpan InactivityThreshold { get; set; } = System.TimeSpan.FromMinutes(5);

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
