// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Abstractions.Serialization;

/// <summary>
/// Marks a field or property as part of the serialized header section.
/// Header members are ordered before regular payload members.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public class SerializeHeaderAttribute : Attribute
{
    /// <summary>Gets the explicit order used among header members.</summary>
    public int Order { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SerializeHeaderAttribute"/> class.</summary>
    /// <param name="order">The explicit header order for the member.</param>
    public SerializeHeaderAttribute(int order) => this.Order = order;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeHeaderAttribute"/> class
    /// using a packet header offset value.
    /// </summary>
    /// <param name="position">The packet header offset that maps to the header order.</param>
    public SerializeHeaderAttribute(PacketHeaderOffset position) : this((int)position) { }
}
