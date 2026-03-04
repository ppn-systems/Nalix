// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Defines the reserved OpCodes for Nalix system and protocol-level internal packets.
/// Values in the range 0x0000-0x00FF are reserved for system use.
/// </summary>
public enum ProtocolOpCode : ushort
{
    /// <summary>
    /// The default handshake protocol packet for key exchange and transcript verification.
    /// </summary>
    HANDSHAKE = 0x0000,

    /// <summary>
    /// Used for system-level control packets like PING, PONG, ERROR, DISCONNECT.
    /// </summary>
    SYSTEM_CONTROL = 0x0001,

    /// <summary>
    /// Unified packet flow for session management (resume, ack, reject).
    /// </summary>
    SESSION_SIGNAL = 0x0002,
}
