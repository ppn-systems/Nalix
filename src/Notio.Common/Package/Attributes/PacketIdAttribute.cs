using System;

namespace Notio.Common.Package.Attributes;

/// <summary>
/// Custom attribute to specify a packet identifier for a class.
/// </summary>
/// <remarks>
/// This attribute is applied to method to assign a unique packet identifier.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketIdAttribute"/> method.
/// </remarks>
/// <param name="id">The unique identifier for the packet associated with the method.</param>
[AttributeUsage(AttributeTargets.Method)]
public class PacketIdAttribute(ushort id) : Attribute
{
    /// <summary>
    /// Gets the packet identifier associated with the method.
    /// </summary>
    public ushort Id { get; } = id;
}
