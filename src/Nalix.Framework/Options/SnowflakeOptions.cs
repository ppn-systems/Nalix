// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared.Attributes;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Identifier configuration options.
/// </summary>
public class SnowflakeOptions : ConfigurationLoader
{
    /// <summary>
    /// Machine ID (1-1023) used in distributed ID generation.
    /// </summary>
    [IniComment("Machine ID (1-1023) used in distributed ID generation.")]
    public System.UInt16 MachineId { get; set; } = 1;
}
