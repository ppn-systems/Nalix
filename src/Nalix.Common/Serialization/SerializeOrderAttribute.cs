// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Networking.Packets;

namespace Nalix.Common.Serialization;

/// <summary>
/// Specifies that a field or property should be included in serialization, with a defined order.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public class SerializeOrderAttribute : Attribute
{
    /// <summary>
    /// Gets the serialization order of the field or property.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class with the specified serialization order.
    /// </summary>
    /// <param name="order">The order in which the field or property should be serialized.</param>
    [SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public SerializeOrderAttribute(int order) => Order = order;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class using an enum value for serialization order.
    /// </summary>
    /// <param name="position">The enum value that defines the order of serialization.</param>
    public SerializeOrderAttribute(PacketHeaderOffset position) : this((int)position) { }
}
