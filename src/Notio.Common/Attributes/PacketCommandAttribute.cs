using System;

namespace Notio.Common.Attributes;

/// <summary>
/// An attribute used to define a packet command and its required authority level.
/// This attribute is applied to methods to associate them with specific packet commands.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PacketCommandAttribute : Attribute
{
    /// <summary>
    /// Gets the unique command identifier for the packet.
    /// </summary>
    /// <remarks>
    /// The <see cref="Command"/> value is assigned either through a numeric identifier 
    /// or via an enum with an underlying type of <see cref="ushort"/>.
    /// </remarks>
    public ushort Command { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCommandAttribute"/> class 
    /// using a numeric command identifier.
    /// </summary>
    /// <param name="command">
    /// The numeric identifier for the command. This value is typically 
    /// used when the command is represented by a constant.
    /// </param>
    public PacketCommandAttribute(ushort command) => Command = command;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCommandAttribute"/> class 
    /// using an enum with <see cref="ushort"/> as the underlying type.
    /// </summary>
    /// <param name="command">
    /// The enum value representing the command. The enum must have <see cref="ushort"/> 
    /// as its underlying type. If the enum is not valid, an exception will be thrown.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="command"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="command"/> is not of type <see cref="Enum"/> or 
    /// does not have <see cref="ushort"/> as its underlying type.
    /// </exception>
    public PacketCommandAttribute(Enum command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command), $"{nameof(command)} cannot be null.");

        if (command is not Enum enumCommand)
            throw new ArgumentException($"{nameof(command)} must be an Enum type.", nameof(command));

        if (Enum.GetUnderlyingType(enumCommand.GetType()) != typeof(ushort))
            throw new ArgumentException(
                $"{nameof(command)} must have {typeof(ushort).Name} as its underlying type.", nameof(command));

        Command = Convert.ToUInt16(enumCommand);
    }
}
