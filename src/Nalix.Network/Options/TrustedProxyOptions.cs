// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration options for trusted reverse proxies (e.g., Cloudflare, NGINX).
/// </summary>
[IniComment("Trusted proxy configuration — allows CDN/proxies to bypass standard limits but enforces ceilings")]
public sealed partial class TrustedProxyOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets a comma-separated list of trusted proxy IP networks (e.g. Cloudflare IPs).
    /// These IPs will bypass normal limits and use the trusted ceilings instead.
    /// Example: "103.21.244.0/22,103.22.200.0/22".
    /// </summary>
    [IniComment("Comma-separated list of trusted proxy CIDRs/IPs (e.g. Cloudflare). These IPs use the trusted proxy ceilings instead of normal limits.")]
    public string TrustedProxiesString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed per trusted proxy.
    /// </summary>
    [IniComment("Max concurrent connections from a single trusted proxy (1–100,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 100_000, ErrorMessage = "MaxConnectionsPerTrustedProxy must be between 1 and 100,000.")]
    public int MaxConnectionsPerTrustedProxy { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the maximum number of connection attempts allowed within the configured rate window for a trusted proxy.
    /// </summary>
    [IniComment("Max connection attempts from a trusted proxy within the rate window (1–10,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "MaxAttemptsPerTrustedProxyWindow must be between 1 and 10,000,000.")]
    public int MaxAttemptsPerTrustedProxyWindow { get; set; } = 2000;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        if (this.MaxConnectionsPerTrustedProxy < 1 || this.MaxConnectionsPerTrustedProxy > 100_000)
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.MaxConnectionsPerTrustedProxy), "MaxConnectionsPerTrustedProxy must be between 1 and 100,000.");
        }

        if (this.MaxAttemptsPerTrustedProxyWindow < 1 || this.MaxAttemptsPerTrustedProxyWindow > 10_000_000)
        {
            throw new System.ArgumentOutOfRangeException(nameof(this.MaxAttemptsPerTrustedProxyWindow), "MaxAttemptsPerTrustedProxyWindow must be between 1 and 10,000,000.");
        }
    }
}
