// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Abstractions.Serialization;

/// <summary>
/// Marks a field or property with an explicit serialization order.
/// This is used when the serializer should not infer ordering automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public class SerializeOrderAttribute : Attribute
{
    /// <summary>Gets the explicit serialization order assigned to the member.</summary>
    public int Order { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class.</summary>
    /// <param name="order">The explicit order value to assign to the member.</param>
    public SerializeOrderAttribute(int order) => this.Order = order;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class
    /// using a packet header offset value.
    /// </summary>
    /// <param name="position">The packet header offset that maps to the explicit order.</param>
    public SerializeOrderAttribute(PacketHeaderOffset position) : this((int)position) { }
}
