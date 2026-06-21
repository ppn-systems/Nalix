// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace ChatRoom.Contracts;

/// <summary>
/// A chat message packet exchanged between clients via the server.
/// <para>
/// Uses opcode <c>0x7101</c>, which is in the user-defined sample range
/// and does not conflict with built-in protocol opcodes.
/// </para>
/// </summary>
[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class ChatMessagePacket : PacketBase<ChatMessagePacket>, IPacketStaticOpcode
{
    /// <summary>
    /// The opcode that identifies this packet type on the wire.
    /// </summary>
    public static ushort StaticOpCode => 0x7101;

    /// <summary>
    /// Gets or sets the display name of the user who sent the message.
    /// </summary>
    [SerializeOrder(0)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chat message text.
    /// </summary>
    [SerializeOrder(1)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="ChatMessagePacket"/> with empty fields.
    /// </summary>
    public ChatMessagePacket() { }
}
