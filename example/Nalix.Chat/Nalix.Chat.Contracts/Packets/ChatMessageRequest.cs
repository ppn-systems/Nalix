// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

namespace Nalix.Chat.Contracts.Packets;

/// <summary>
/// Sends a chat message to a room.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class ChatMessageRequest : PacketBase<ChatMessageRequest>
{
    /// <summary>
    /// Transport opcode value.
    /// </summary>
    public const ushort OpCodeValue = (ushort)ChatOpCode.ChatMessageRequest;

    /// <summary>
    /// Gets or sets room identifier.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(0)]
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets participant identifier.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(1)]
    public string ParticipantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets client-side message correlation id.
    /// </summary>
    [SerializeOrder(2)]
    public long ClientMessageId { get; set; }

    /// <summary>
    /// Gets or sets message payload text.
    /// </summary>
    [SerializeDynamicSize(1024)]
    [SerializeOrder(3)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client timestamp in unix milliseconds.
    /// </summary>
    [SerializeOrder(4)]
    public long ClientTimestampUnixMs { get; set; }

    /// <summary>
    /// Initializes a new request packet.
    /// </summary>
    public ChatMessageRequest() => this.OpCode = OpCodeValue;
}
