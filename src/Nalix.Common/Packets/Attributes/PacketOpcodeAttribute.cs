// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Specifies the operation code (OpCode) that identifies the type of packet handled by the target method.
/// </summary>
/// <remarks>
/// Apply this attribute to a method to associate it with a unique packet OpCode.
/// The OpCode is typically used by a packet dispatcher to route incoming packets
/// to the correct handler.
/// </remarks>
/// <param name="opcode">
/// The unique operation code that identifies the packet type.
/// </param>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketOpcodeAttribute(System.UInt16 opcode) : System.Attribute
{
    /// <summary>
    /// Gets the operation code associated with the target method.
    /// </summary>
    public System.UInt16 OpCode { get; } = opcode;
}
