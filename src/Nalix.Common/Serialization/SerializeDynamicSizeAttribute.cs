// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Serialization;

/// <summary>
/// Indicates that a field/property has dynamic size during serialization.
/// Provides hints for buffer allocation and performance optimization.
/// Follows Domain-Driven Design principles for serialization metadata.
/// </summary>
/// <param name="size">Expected average size for optimization.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public sealed class SerializeDynamicSizeAttribute(int size) : Attribute
{
    /// <summary>
    /// Expected average size in bytes for buffer pre-allocation.
    /// Helps optimize performance by reducing memory allocations.
    /// </summary>
    public int Size { get; init; } = size;

    /// <summary>
    /// Initializes a new instance with default settings.
    /// </summary>
    public SerializeDynamicSizeAttribute()
        : this(20 * 4) // Default size hint, can be adjusted based on expected data size
    { }
}
