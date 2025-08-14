// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Serialization;

/// <summary>
/// Defines constant values used during serialization to represent special cases
/// such as null values and maximum size limits.
/// </summary>
public static class SerializerBounds
{
    /// <summary>
    /// Special marker for a <c>null</c> value.
    /// Equal to <see cref="System.UInt16.MaxValue"/> (65535).
    /// </summary>
    public const System.UInt16 Null = System.UInt16.MaxValue;

    /// <summary>
    /// Maximum allowed array size.
    /// Equal to <see cref="System.UInt16.MaxValue"/> - 1 (65534).
    /// </summary>
    public const System.UInt16 MaxArray = System.UInt16.MaxValue - 1;

    /// <summary>
    /// Maximum allowed string length.
    /// Equal to <see cref="System.UInt16.MaxValue"/> - 2 (65533).
    /// </summary>
    public const System.UInt16 MaxString = System.UInt16.MaxValue - 2;
}
