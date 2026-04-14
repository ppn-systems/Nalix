// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

namespace Nalix.Chat.Contracts.Events;

/// <summary>
/// Broadcasts a room message to subscribers.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class ChatMessageBroadcast : PacketBase<ChatMessageBroadcast>
{
    /// <summary>
    /// Transport opcode value.
    /// </summary>
    public const ushort OpCodeValue = (ushort)ChatOpCode.ChatMessageBroadcast;

    /// <summary>
    /// Gets or sets room identifier.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(0)]
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets server message id.
    /// </summary>
    [SerializeOrder(1)]
    public long ServerMessageId { get; set; }

    /// <summary>
    /// Gets or sets sender participant id.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(2)]
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets sender display name.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(3)]
    public string SenderDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets message payload.
    /// </summary>
    [SerializeDynamicSize(1024)]
    [SerializeOrder(4)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets server timestamp in unix milliseconds.
    /// </summary>
    [SerializeOrder(5)]
    public long ServerTimestampUnixMs { get; set; }

    /// <summary>
    /// Initializes a new broadcast packet.
    /// </summary>
    public ChatMessageBroadcast() => this.OpCode = OpCodeValue;
}
