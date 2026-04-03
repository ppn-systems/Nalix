// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Serialization;

/// <summary>
/// Marks a type whose serialized size is known at compile time.
/// The serializer can use this to avoid per-instance size discovery.
/// </summary>
public interface IFixedSizeSerializable
{
    /// <summary>
    /// Gets the fixed size, in bytes, required to serialize one instance of the type.
    /// </summary>
    static abstract int Size { get; }
}
