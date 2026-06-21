// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace HelloWorld.Contracts;

/// <summary>
/// A simple request packet sent from the client to the server.
/// <para>
/// Uses opcode <c>0x7001</c>, which is in the user-defined sample range
/// and does not conflict with built-in protocol opcodes.
/// </para>
/// </summary>
[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class HelloRequestPacket
    : PacketBase<HelloRequestPacket>,
      IFixedSizeSerializable,
      IPacketStaticOpcode
{
    /// <summary>
    /// The opcode that identifies this packet type on the wire.
    /// </summary>
    public static ushort StaticOpCode => 0x7001;

    /// <summary>
    /// Gets or sets the greeting type.
    /// A value of <c>1</c> represents "Hello".
    /// </summary>
    [SerializeOrder(0)]
    public byte Greeting { get; set; }

    /// <summary>
    /// Initializes a new <see cref="HelloRequestPacket"/> with the default "Hello" greeting.
    /// </summary>
    public HelloRequestPacket() => this.Greeting = 1;
}
