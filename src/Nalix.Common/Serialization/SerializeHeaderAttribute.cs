// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;

namespace Nalix.Common.Serialization;

/// <summary>
/// Specifies that a field or property should be included in serialization as a header, meaning it will be ordered before regular ordered fields.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public class SerializeHeaderAttribute : Attribute
{
    /// <summary>
    /// Gets the serialization order of the header field or property.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeHeaderAttribute"/> class with the specified serialization order.
    /// </summary>
    /// <param name="order">The order in which the header field or property should be serialized.</param>
    public SerializeHeaderAttribute(int order) => this.Order = order;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeHeaderAttribute"/> class using an enum value for serialization order.
    /// </summary>
    /// <param name="position">The enum value that defines the order of serialization.</param>
    public SerializeHeaderAttribute(PacketHeaderOffset position) : this((int)position) { }
}
