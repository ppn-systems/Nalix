// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Marks a method with the opcode that identifies its packet.
/// </summary>
/// <remarks>
/// The dispatcher uses this value to route incoming packets to the correct handler.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketOpcodeAttribute : Attribute
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketOpcodeAttribute"/> class.
    /// </summary>
    /// <param name="opcode">
    /// The unique operation code that identifies the packet type.
    /// </param>
    public PacketOpcodeAttribute(ushort opcode) => this.OpCode = opcode;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketOpcodeAttribute"/> class.
    /// </summary>
    /// <param name="opcode">
    /// The enum value that identifies the packet type.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="opcode"/> is not an enum value.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the enum value does not fit in a <see cref="ushort"/>.
    /// </exception>
    public PacketOpcodeAttribute(object opcode)
    {
        ArgumentNullException.ThrowIfNull(opcode);

        Type opcodeType = opcode.GetType();

        if (!opcodeType.IsEnum)
        {
            throw new ArgumentException("The opcode value must be an enum value.", nameof(opcode));
        }

        ulong value = Convert.ToUInt64(opcode, CultureInfo.InvariantCulture);

        if (value > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(opcode),
                opcode,
                "The opcode enum value must fit in UInt16.");
        }

        this.OpCode = (ushort)value;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets the opcode associated with the target method.
    /// </summary>
    public ushort OpCode { get; }

    #endregion Properties
}
