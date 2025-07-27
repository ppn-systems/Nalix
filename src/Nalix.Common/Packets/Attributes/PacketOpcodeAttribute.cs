namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Custom attribute to specify a packet identifier for a class.
/// </summary>
/// <remarks>
/// This attribute is applied to method to assign a unique packet identifier.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketOpcodeAttribute"/> method.
/// </remarks>
/// <param name="opcode">The unique identifier for the packet associated with the method.</param>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class PacketOpcodeAttribute(System.UInt16 opcode) : System.Attribute
{
    /// <summary>
    /// Gets the packet identifier associated with the method.
    /// </summary>
    public System.UInt16 OpCode { get; } = opcode;
}
