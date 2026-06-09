// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Configuration for inbound directive anti-spam guard behavior.
/// </summary>
[IniComment("Inbound directive guard configuration — controls cooldown used to suppress repeated fail/timeout responses")]
public sealed partial class DirectiveGuardOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Minimum interval (milliseconds) between repeated directives of the same category per connection.
    /// Set to 0 to disable suppression.
    /// </summary>
    [IniComment("Minimum cooldown in milliseconds for repeated inbound directives (0 = disabled, default 1000)")]
    [ValueRange(0, 60000)]
    public int DefaultCooldownMs { get; set; } = 1_000;

    /// <summary>
    /// Validates option values.
    /// </summary>
    public void Validate() => this.ValidateDataAnnotations();
}
