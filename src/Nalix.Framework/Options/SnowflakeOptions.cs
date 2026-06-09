// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Identifier configuration options.
/// </summary>
[IniComment("Identifier configuration — controls distributed ID generation machine mapping")]
public sealed partial class SnowflakeOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Machine ID (1-1023) used in distributed ID generation.
    /// </summary>
    [IniComment("Machine ID (1-1023) used in distributed ID generation.")]
    [ValueRange(1, 1023)]
    public ushort MachineId { get; set; } = 1;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate() => this.ValidateDataAnnotations();
}
