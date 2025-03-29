using System;

namespace Notio.Common.Attributes;

/// <summary>
/// Attribute to define a packet command and its required authority level.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PacketCommandAttribute(ushort command) : Attribute
{
    /// <summary>
    /// The unique command identifier for the packet.
    /// </summary>
    public ushort CommandId { get; } = command;

    /// <summary>
    /// Creates a PacketCommandAttribute with a command from an enum with ushort as underlying type.
    /// </summary>
    public static PacketCommandAttribute Create<TEnum>(TEnum command)
        where TEnum : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(TEnum)) != typeof(ushort))
            throw new ArgumentException("Enum must have ushort as underlying type.", nameof(command));

        return new PacketCommandAttribute(Convert.ToUInt16(command));
    }
}
