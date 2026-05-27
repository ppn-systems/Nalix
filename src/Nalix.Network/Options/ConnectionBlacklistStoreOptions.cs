// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Configuration options for the persistent permanent IP blacklist store.
/// </summary>
[IniComment("Configuration for persisting blacklisted IP addresses to disk")]
public sealed partial class ConnectionBlacklistStoreOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether disk persistence is enabled for the blacklist.
    /// Default is true.
    /// </summary>
    [IniComment("Enable loading blacklisted IPs from disk (default true)")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the file name used for storing blacklisted IPs.
    /// Default is "blacklist.txt".
    /// </summary>
    [IniComment("File name for storing blacklisted IPs in the data directory (default blacklist.txt)")]
    public string StoreFileName { get; set; } = "blacklist.txt";

    /// <summary>
    /// Gets or sets the maximum number of blacklisted IPs to load from disk.
    /// </summary>
    [IniComment("Maximum number of blacklisted IPs to load from disk (10-1,000,000, default 100,000)")]
    [System.ComponentModel.DataAnnotations.Range(10, 1_000_000, ErrorMessage = "MaxBlacklistedIps must be between 10 and 1,000,000.")]
    public int MaxBlacklistedIps { get; set; } = 100_000;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    public void Validate()
    {
        this.ValidateDataAnnotations();
    }
}
