// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Serialization;

/// <summary>
/// Marks a field or property whose serialized size is not fixed at compile time.
/// The size hint helps serializers preallocate buffers more accurately.
/// </summary>
/// <param name="size">The initial size hint, in bytes, used for buffer preallocation.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public sealed class SerializeDynamicSizeAttribute(int size) : Attribute
{
    /// <summary>Gets the size hint, in bytes, used for buffer preallocation.</summary>
    public int Size { get; set; } = size;

    /// <summary>Initializes a new instance with a default size hint.</summary>
    public SerializeDynamicSizeAttribute()
        : this(20 * 4)
    { }
}
