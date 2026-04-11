// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Shared;

/// <summary>
/// Defines the OpCodes for the Nalix Secure Chat application.
/// Values start from 0x0100 to avoid collision with reserved ProtocolOpCodes (0x0000-0x00FF).
/// </summary>
public enum ChatOpCode : ushort
{
    /// <summary>
    /// User requesting to send a message to the room.
    /// </summary>
    MESSAGE_REQUEST = 0x0110,

    /// <summary>
    /// Server broadcasting a message event to all room members.
    /// </summary>
    MESSAGE_EVENT = 0x0111,

    /// <summary>
    /// User joining the chat room.
    /// </summary>
    JOIN_REQUEST = 0x0112,

    /// <summary>
    /// Server notifying members that someone has joined.
    /// </summary>
    JOIN_EVENT = 0x0113,

    /// <summary>
    /// Server notifying members that someone has left.
    /// </summary>
    LEAVE_EVENT = 0x0114
}
