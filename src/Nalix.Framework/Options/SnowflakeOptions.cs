// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Identifier configuration options.
/// </summary>
[IniComment("Identifier configuration — controls distributed ID generation machine mapping")]
public sealed class SnowflakeOptions : ConfigurationLoader
{
    /// <summary>
    /// Machine ID (1-1023) used in distributed ID generation.
    /// </summary>
    [IniComment("Machine ID (1-1023) used in distributed ID generation.")]
    [Range(1, 1023, ErrorMessage = "MachineId must be between 1 and 1023.")]
    public ushort MachineId { get; set; } = 1;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        ValidationContext context = new(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
