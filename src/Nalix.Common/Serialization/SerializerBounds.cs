// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Serialization;

/// <summary>
/// Defines constant values used during serialization to represent special cases
/// such as null values and maximum size limits.
/// </summary>
public static class SerializerBounds
{
    /// <summary>
    /// Special marker for a <c>null</c> value.
    /// Equal to <see cref="ushort.MaxValue"/> (65535).
    /// </summary>
    public const ushort Null = ushort.MaxValue;

    /// <summary>
    /// Maximum allowed array size.
    /// Equal to <see cref="ushort.MaxValue"/> - 1 (65534).
    /// </summary>
    public const ushort MaxArray = ushort.MaxValue - 1;

    /// <summary>
    /// Maximum allowed string length.
    /// Equal to <see cref="ushort.MaxValue"/> - 2 (65533).
    /// </summary>
    public const ushort MaxString = ushort.MaxValue - 2;

    /// <summary>
    /// Special marker for a <c>null</c> array.
    /// Consists of four bytes with value 255: [255, 255, 255, 255].
    /// </summary>
    public static ReadOnlySpan<byte> NullArrayMarker => [255, 255, 255, 255];

    /// <summary>
    /// Special marker for an empty array.
    /// Consists of four zero bytes: [0, 0, 0, 0].
    /// </summary>
    public static ReadOnlySpan<byte> EmptyArrayMarker => [0, 0, 0, 0];
}
