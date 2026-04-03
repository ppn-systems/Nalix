// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;

namespace Nalix.Common.Serialization;

/// <summary>
/// Specifies the serialization order of a field or property.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public class SerializeOrderAttribute : Attribute
{
    /// <summary>Gets the serialization order of the field or property.</summary>
    public int Order { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class.</summary>
    /// <param name="order">The order in which the field or property should be serialized.</param>
    public SerializeOrderAttribute(int order) => this.Order = order;

    /// <summary>Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class using a packet header offset.</summary>
    /// <param name="position">The enum value that defines the order of serialization.</param>
    public SerializeOrderAttribute(PacketHeaderOffset position) : this((int)position) { }
}
