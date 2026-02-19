// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Serialization;

/// <summary>
/// Defines the layout strategy for serialization.
/// </summary>
public enum SerializeLayout : byte
{
    /// <summary>
    /// Indicates that serialization should follow an automatic layout,
    /// where fields are grouped and ordered by size to minimize layout memory padding.
    /// This is the default layout.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Indicates that serialization should follow a sequential layout,
    /// where fields or properties are processed in the order they are defined.
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// Indicates that serialization should follow an explicit layout,
    /// where the serialization order or structure is explicitly defined.
    /// </summary>
    Explicit = 2
}
