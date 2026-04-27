// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Codec.Options;

/// <summary>
/// Security-related configuration for hashing and encryption.
/// </summary>
[IniComment("Security configuration — controls hashing iterations and other cryptographic parameters")]
public sealed class SecurityOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the iteration count for PBKDF2 hashing.
    /// Higher values are more secure but slower. Default: 310,000.
    /// </summary>
    [IniComment("Iteration count for PBKDF2 hashing (default 310,000)")]
    [System.ComponentModel.DataAnnotations.Range(1000, 10_000_000)]
    public int Pbkdf2Iterations { get; set; } = 310_000;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
