// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Enums;

namespace Nalix.Common.Serialization.Attributes;

/// <summary>
/// Specifies that a field or property should be included in serialization, with a defined order.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, Inherited = true)]
public class SerializeOrderAttribute : System.Attribute
{
    /// <summary>
    /// Gets the serialization order of the field or property.
    /// </summary>
    public System.Int32 Order { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class with the specified serialization order.
    /// </summary>
    /// <param name="order">The order in which the field or property should be serialized.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public SerializeOrderAttribute(System.Int32 order) => Order = order;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializeOrderAttribute"/> class using an enum value for serialization order.
    /// </summary>
    /// <param name="position">The enum value that defines the order of serialization.</param>
    public SerializeOrderAttribute(PacketHeaderOffset position) : this((System.Byte)position) { }
}
