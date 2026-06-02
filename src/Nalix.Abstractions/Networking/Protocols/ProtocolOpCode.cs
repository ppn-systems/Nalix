// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Protocols;

/// <summary>
/// Defines the reserved OpCodes for Nalix system and protocol-level internal packets.
/// Values in the range 0x0000-0x00FF are reserved for system use.
/// </summary>
public enum ProtocolOpCode : ushort
{
    /// <summary>
    /// Client initiates the handshake and sends its ephemeral public key.
    /// </summary>
    SESSION_INIT = 0x0000,

    /// <summary>
    /// Used for system-level control packets like PING, PONG, ERROR, DISCONNECT.
    /// </summary>
    SYSTEM_CONTROL = 0x0001,

    /// <summary>
    /// Unified packet flow for session management (resume, ack, reject).
    /// </summary>
    SESSION_SIGNAL = 0x0002,

    // SESSION_TOFU is intentionally removed.
    // SessionTofu packet is purely Server->Client and uses SYSTEM_CONTROL OpCode for transmission, 
    // relying on its MagicNumber for deserialization.

    // SESSION_CHALLENGE is intentionally removed (uses SYSTEM_CONTROL).

    /// <summary>
    /// Client confirms the derived transcript and proves possession.
    /// </summary>
    SESSION_PROOF = 0x0003,

    /// <summary>
    /// Time synchronization and PING packets.
    /// </summary>
    SYSTEM_TIMESYNC = 0x0004,
}
