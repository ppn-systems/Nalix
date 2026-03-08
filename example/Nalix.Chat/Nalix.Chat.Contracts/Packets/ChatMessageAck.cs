// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Contracts.Enums;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

namespace Nalix.Chat.Contracts.Packets;

/// <summary>
/// Acknowledges a chat message request.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class ChatMessageAck : PacketBase<ChatMessageAck>
{
    /// <summary>
    /// Transport opcode value.
    /// </summary>
    public const ushort OpCodeValue = (ushort)ChatOpCode.ChatMessageAck;

    /// <summary>
    /// Gets or sets client message id.
    /// </summary>
    [SerializeOrder(0)]
    public long ClientMessageId { get; set; }

    /// <summary>
    /// Gets or sets server generated message id.
    /// </summary>
    [SerializeOrder(1)]
    public long ServerMessageId { get; set; }

    /// <summary>
    /// Gets or sets whether send succeeded.
    /// </summary>
    [SerializeOrder(2)]
    public bool Accepted { get; set; }

    /// <summary>
    /// Gets or sets transport-safe error code.
    /// </summary>
    [SerializeOrder(3)]
    public ChatErrorCode ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets optional user-facing message.
    /// </summary>
    [SerializeDynamicSize(128)]
    [SerializeOrder(4)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets server timestamp in unix milliseconds.
    /// </summary>
    [SerializeOrder(5)]
    public long ServerTimestampUnixMs { get; set; }

    /// <summary>
    /// Initializes a new acknowledgement packet.
    /// </summary>
    public ChatMessageAck() => this.OpCode = OpCodeValue;
}
