// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Serialization;

/// <summary>
/// Describes how fields are ordered when a type is serialized.
/// </summary>
public enum SerializeLayout : byte
{
    /// <summary>
    /// Fields are grouped and ordered automatically to reduce padding and improve packing.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Fields or properties are processed in the order they are declared.
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// Field order is taken from explicit serialization metadata.
    /// </summary>
    Explicit = 2
}
