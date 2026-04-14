// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

namespace Nalix.Chat.Contracts.Packets;

/// <summary>
/// Requests to join a chat room.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class JoinRoomRequest : PacketBase<JoinRoomRequest>
{
    /// <summary>
    /// Transport opcode value.
    /// </summary>
    public const ushort OpCodeValue = (ushort)ChatOpCode.JoinRoomRequest;

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
    /// Gets or sets participant display name.
    /// </summary>
    [SerializeDynamicSize(64)]
    [SerializeOrder(2)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets client request identifier used for correlation.
    /// </summary>
    [SerializeOrder(3)]
    public long ClientRequestId { get; set; }

    /// <summary>
    /// Initializes a new request packet.
    /// </summary>
    public JoinRoomRequest() => this.OpCode = OpCodeValue;
}
