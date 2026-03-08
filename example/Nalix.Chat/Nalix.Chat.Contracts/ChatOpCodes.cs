// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Contracts;

/// <summary>
/// Defines operation codes used by the secure chat sample.
/// </summary>
public enum ChatOpCode : ushort
{
    /// <summary>
    /// Join room request packet.
    /// </summary>
    JoinRoomRequest = 0x0110,

    /// <summary>
    /// Join room response packet.
    /// </summary>
    JoinRoomResponse = 0x0111,

    /// <summary>
    /// Send message request packet.
    /// </summary>
    ChatMessageRequest = 0x0112,

    /// <summary>
    /// Send message acknowledgement packet.
    /// </summary>
    ChatMessageAck = 0x0113,

    /// <summary>
    /// Broadcast message event packet.
    /// </summary>
    ChatMessageBroadcast = 0x0114
}
