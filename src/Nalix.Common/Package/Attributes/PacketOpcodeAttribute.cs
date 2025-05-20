namespace Nalix.Common.Package.Attributes;

/// <summary>
/// Custom attribute to specify a packet identifier for a class.
/// </summary>
/// <remarks>
/// This attribute is applied to method to assign a unique packet identifier.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketOpcodeAttribute"/> method.
/// </remarks>
/// <param name="id">The unique identifier for the packet associated with the method.</param>
[System.AttributeUsage(System.AttributeTargets.Method)]
public class PacketOpcodeAttribute(ushort id) : System.Attribute
{
    /// <summary>
    /// Gets the packet identifier associated with the method.
    /// </summary>
    public ushort Id { get; } = id;
}
