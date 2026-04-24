// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Marks a method with the opcode that identifies its packet.
/// </summary>
/// <remarks>
/// The dispatcher uses this value to route incoming packets to the correct handler.
/// </remarks>
/// <param name="opcode">
/// The unique operation code that identifies the packet type.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketOpcodeAttribute(ushort opcode) : Attribute
{
    /// <summary>
    /// Gets the opcode associated with the target method.
    /// </summary>
    public ushort OpCode { get; } = opcode;
}
