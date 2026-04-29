// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

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
    /// Gets or sets the maximum size (in bytes) allowed for a single UDP datagram.
    /// Default 1400 avoids IP fragmentation.
    /// </summary>
    [IniComment("Maximum allowed UDP datagram size in bytes to avoid fragmentation (default 1400)")]
    [System.ComponentModel.DataAnnotations.Range(64, 65507, ErrorMessage = "MaxUdpDatagramSize must be between 64 and 65507.")]
    public int MaxUdpDatagramSize { get; set; } = 1400;

    /// <summary>
    /// Gets or sets the maximum allowed error count before a connection is automatically severed.
    /// SEC-54: Prevents persistent noisy or malformed connections from consuming CPU/logs.
    /// </summary>
    [IniComment("Maximum cumulative errors allowed per connection before disconnection (SEC-54, default 50)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "MaxErrorThreshold must be at least 1.")]
    public int MaxErrorThreshold { get; set; } = 50;

    /// <summary>
    /// Gets or sets the bit-size of the UDP replay protection sliding window.
    /// SEC-27: Larger windows consume more memory but are more resilient to packet out-of-order arrival.
    /// </summary>
    [IniComment("UDP replay protection window size in bits (default 1024)")]
    [System.ComponentModel.DataAnnotations.Range(64, 65536, ErrorMessage = "UdpReplayWindowSize must be between 64 and 65536.")]
    public int UdpReplayWindowSize { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the maximum number of packets allowed per second from a single connection before it is considered abusive and disconnected.
    /// </summary>
    [IniComment("Maximum packets per second allowed from a single connection (SEC-55, default 128)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "MaxPacketPerSecond must be between 1 and 10,000,000.")]
    public int MaxPacketPerSecond { get; set; } = 128;

    /// <summary>
    /// Gets or sets the maximum number of expired entries to remove in a single cleanup cycle.
    /// If set to 0 (default), it will automatically scale to remove a percentage of the tracked entries.
    /// </summary>
    [IniComment("Max entries to clean per cycle. 0 = auto scale based on load (default 0)")]
    [System.ComponentModel.DataAnnotations.Range(0, 10_000_000, ErrorMessage = "MaxCleanupKeysPerRun must be between 0 and 10,000,000.")]
    public int MaxCleanupKeysPerRun { get; set; } = 0;

    /// <summary>
    /// Gets or sets the UTC offset to use when determining the start of a new day for connection limits.
    /// Default is TimeSpan.Zero (00:00 UTC).
    /// </summary>
    [IniComment("UTC offset for the daily connection limit reset. Example: 07:00:00 for GMT+7 (default 00:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "-14:00:00", "14:00:00", ErrorMessage = "DailyResetTimeOffset must be between -14:00:00 and 14:00:00.")]
    public System.TimeSpan DailyResetTimeOffset { get; set; } = System.TimeSpan.Zero;

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
