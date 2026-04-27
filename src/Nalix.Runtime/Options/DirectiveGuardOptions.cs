// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Common.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Configuration for inbound directive anti-spam guard behavior.
/// </summary>
[IniComment("Inbound directive guard configuration — controls cooldown used to suppress repeated fail/timeout responses")]
public sealed class DirectiveGuardOptions : ConfigurationLoader
{
    /// <summary>
    /// Minimum interval (milliseconds) between repeated directives of the same category per connection.
    /// Set to 0 to disable suppression.
    /// </summary>
    [IniComment("Minimum cooldown in milliseconds for repeated inbound directives (0 = disabled, default 200)")]
    [Range(0, 60000, ErrorMessage = "DefaultCooldownMs must be between 0 and 60000.")]
    public int DefaultCooldownMs { get; set; } = 200;

    /// <summary>
    /// Validates option values.
    /// </summary>
    public void Validate()
    {
        ValidationContext context = new(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}

