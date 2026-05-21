// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration options for connection protection against abuse.
/// </summary>
[IniComment("Connection protection — limits that prevent abuse (errors, packet spam, progressive bans)")]
public sealed partial class ConnectionGuardOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed globally.
    /// </summary>
    [IniComment("Maximum concurrent connections across all IPs (-1 = unlimited, default -1)")]
    [System.ComponentModel.DataAnnotations.Range(-1, int.MaxValue, ErrorMessage = "MaxConnections must be -1 (unlimited) or positive.")]
    public int MaxConnections { get; set; } = -1;

    /// <summary>
    /// Gets or sets the ban duration for IPs that exceed the connection rate limit.
    /// </summary>
    [IniComment("How long an IP is banned after exceeding limits (00:00:01–1.00:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "1.00:00:00", ErrorMessage = "BanDuration must be at least 1 second and at most 1 day.")]
    public System.TimeSpan BanDuration { get; set; } = System.TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the suppression window for DDoS-related log entries.
    /// </summary>
    [IniComment("Suppresses repeated DDoS log entries from the same IP within this window (00:00:01–01:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "DDoSLogSuppressWindow must be between 1 second and 1 hour.")]
    public System.TimeSpan DDoSLogSuppressWindow { get; set; } = System.TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets or sets the maximum allowed error count before a connection is automatically severed.
    /// SEC-54: Prevents persistent noisy or malformed connections from consuming CPU/logs.
    /// </summary>
    [IniComment("Maximum cumulative errors allowed per connection before disconnection (SEC-54, default 50)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "MaxErrorThreshold must be at least 1.")]
    public int MaxErrorThreshold { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of packets allowed per second from a single connection before it is considered abusive and disconnected.
    /// </summary>
    [IniComment("Maximum packets per second allowed from a single connection (SEC-55, default 128)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "MaxPacketPerSecond must be between 1 and 10,000,000.")]
    public int MaxPacketPerSecond { get; set; } = 128;

    /// <summary>
    /// Gets or sets a comma-separated list of permanently blacklisted IP networks or addresses.
    /// </summary>
    [IniComment("Comma-separated list of permanently blacklisted CIDRs/IPs.")]
    public string BlacklistedIpsString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether progressive banning is enabled.
    /// </summary>
    [IniComment("Enable progressive banning schedules (e.g. ban for 1m, then 5m, then 15m) (default true)")]
    public bool EnableProgressiveBanning { get; set; } = true;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        if (this.MaxConnections < -1 || this.MaxConnections == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.MaxConnections), "MaxConnections must be -1 (unlimited) or positive.");
        }

        if (this.BanDuration < System.TimeSpan.FromSeconds(1) || this.BanDuration > System.TimeSpan.FromDays(1))
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.BanDuration), "BanDuration must be at least 1 second and at most 1 day.");
        }

        if (this.DDoSLogSuppressWindow < System.TimeSpan.FromSeconds(1) || this.DDoSLogSuppressWindow > System.TimeSpan.FromHours(1))
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.DDoSLogSuppressWindow), "DDoSLogSuppressWindow must be between 1 second and 1 hour.");
        }

        if (this.MaxErrorThreshold < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.MaxErrorThreshold), "MaxErrorThreshold must be at least 1.");
        }

        if (this.MaxPacketPerSecond < 1 || this.MaxPacketPerSecond > 10_000_000)
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.MaxPacketPerSecond), "MaxPacketPerSecond must be between 1 and 10,000,000.");
        }
    }
}
