// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Serialization.Attributes;

/// <summary>
/// Indicates that a field/property has dynamic size during serialization.
/// Provides hints for buffer allocation and performance optimization.
/// Follows Domain-Driven Design principles for serialization metadata.
/// </summary>
/// <param name="size">Expected average size for optimization.</param>
[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, Inherited = true)]
public sealed class SerializeDynamicSizeAttribute(System.Int32 size) : System.Attribute
{
    /// <summary>
    /// Expected average size in bytes for buffer pre-allocation.
    /// Helps optimize performance by reducing memory allocations.
    /// </summary>
    public System.Int32 Size { get; init; } = size;

    /// <summary>
    /// Initializes a new instance with default settings.
    /// </summary>
    public SerializeDynamicSizeAttribute()
        : this(20 * 4) // Default size hint, can be adjusted based on expected data size
    { }
}
