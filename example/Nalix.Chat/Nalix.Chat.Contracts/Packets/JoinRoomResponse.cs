// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Contracts.Enums;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

namespace Nalix.Chat.Contracts.Packets;

/// <summary>
/// Returns join-room outcome to the client.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class JoinRoomResponse : PacketBase<JoinRoomResponse>
{
    /// <summary>
    /// Transport opcode value.
    /// </summary>
    public const ushort OpCodeValue = (ushort)ChatOpCode.JoinRoomResponse;

    /// <summary>
    /// Gets or sets the client request identifier.
    /// </summary>
    [SerializeOrder(0)]
    public long ClientRequestId { get; set; }

    /// <summary>
    /// Gets or sets whether the request succeeded.
    /// </summary>
    [SerializeOrder(1)]
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    [SerializeOrder(2)]
    public ChatErrorCode ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets canonical room id.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(3)]
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets canonical display name.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(4)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional user-facing message.
    /// </summary>
    [SerializeDynamicSize(128)]
    [SerializeOrder(5)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new response packet.
    /// </summary>
    public JoinRoomResponse() => this.OpCode = OpCodeValue;
}
