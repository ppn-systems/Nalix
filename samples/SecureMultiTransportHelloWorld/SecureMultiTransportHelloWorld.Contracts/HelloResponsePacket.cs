// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace SecureMultiTransportHelloWorld.Contracts;

/// <summary>
/// A simple response packet sent from the server back to the client.
/// <para>
/// Uses opcode <c>0x7202</c>, which is in the user-defined sample range
/// and does not conflict with built-in protocol opcodes.
/// </para>
/// </summary>
[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class HelloResponsePacket
    : PacketBase<HelloResponsePacket>,
      IFixedSizeSerializable,
      IPacketStaticOpcode
{
    /// <summary>
    /// The opcode that identifies this packet type on the wire.
    /// </summary>
    public static ushort StaticOpCode => 0x7202;

    /// <summary>
    /// Gets or sets the reply message identifier.
    /// A value of <c>1</c> means "Hello from Nalix!".
    /// </summary>
    [SerializeOrder(0)]
    public byte Message { get; set; }

    /// <summary>
    /// Initializes a new <see cref="HelloResponsePacket"/> with a default message.
    /// </summary>
    public HelloResponsePacket() => this.Message = 0;
}
