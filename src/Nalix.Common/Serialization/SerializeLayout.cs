// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Serialization;

/// <summary>
/// Defines the layout strategy for serialization.
/// </summary>
public enum SerializeLayout : byte
{
    /// <summary>
    /// Indicates that serialization should follow a sequential layout,
    /// where fields or properties are processed in the order they are defined.
    /// This is the default layout.
    /// </summary>
    Sequential = 0, // default

    /// <summary>
    /// Indicates that serialization should follow an explicit layout,
    /// where the serialization order or structure is explicitly defined.
    /// </summary>
    Explicit = 1
}
