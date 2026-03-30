// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Serialization;

/// <summary>
/// Marks a type as participating in Nalix serialization and selects the layout
/// strategy used for its fields.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = true)]
public sealed class SerializePackableAttribute(SerializeLayout layout = SerializeLayout.Auto) : Attribute
{
    /// <summary>
    /// Gets the layout strategy used when serializing the marked type.
    /// </summary>
    public SerializeLayout SerializeLayout { get; set; } = layout;
}
