// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Serialization;

/// <summary>
/// Defines shared sentinel values and size limits used by serialization code.
/// </summary>
public static class SerializerBounds
{
    /// <summary>
    /// Sentinel value used to encode <see langword="null"/> where a 16-bit length is stored.
    /// </summary>
    public const int Null = -1;

    /// <summary>
    /// Maximum encodable array length when one value is reserved for <see langword="null"/>.
    /// Capped at 1 MB element count to prevent memory-exhaustion DoS from untrusted payloads.
    /// </summary>
    public const int MaxArray = 1_048_576;

    /// <summary>
    /// Maximum encodable UTF-8 string length.
    /// Capped at 1 MB to prevent memory-exhaustion DoS from untrusted payloads.
    /// </summary>
    public const int MaxString = 1_048_576;

    /// <summary>
    /// Sentinel bytes used when a wire format needs to represent a null array.
    /// </summary>
    public static ReadOnlySpan<byte> NullArrayMarker => [255, 255, 255, 255];

    /// <summary>
    /// Sentinel bytes used when a wire format needs to represent an empty array.
    /// </summary>
    public static ReadOnlySpan<byte> EmptyArrayMarker => [0, 0, 0, 0];
}
