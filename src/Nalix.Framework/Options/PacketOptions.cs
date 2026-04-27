// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Common.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Configuration for packet management, including pooling and lifecycle settings.
/// </summary>
[IniComment("Packet configuration — controls pooling strategies for data frames")]
public sealed class PacketOptions : ConfigurationLoader
{
    /// <summary>
    /// Enables object pooling for packets.
    /// When true, packets are rented from ObjectPoolManager. When false, they are instantiated normally.
    /// Server-side applications should keep this enabled for performance.
    /// </summary>
    [IniComment("Enable object pooling for packets (rent from pool vs new instance)")]
    public bool EnablePooling { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        ValidationContext context = new(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
