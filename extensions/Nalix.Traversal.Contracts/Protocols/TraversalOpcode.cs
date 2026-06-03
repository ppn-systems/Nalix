// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Traversal.Protocols;

/// <summary>
/// Defines the Opcodes used by the Nalix.Traversal module.
/// These use the system-reserved range (0x0000 - 0x00FF), allocated top-down 
/// (0x00FF downwards) to avoid collisions with Nalix Core which allocates bottom-up.
/// </summary>
public enum TraversalOpcode : ushort
{
    /// <summary>
    /// Packet for sending/receiving STUN IP/Port information between peers via the server.
    /// </summary>
    PeerSignal = 0x00FF,

    /// <summary>
    /// Packet for NAT probing (Phase 2).
    /// </summary>
    NatProbe = 0x00FE,

    /// <summary>
    /// Acknowledgment for NAT probing (Phase 2).
    /// </summary>
    NatProbeAck = 0x00FD,

    /// <summary>
    /// Client requests Server to allocate a Reflector Session.
    /// </summary>
    ReflectorInit = 0x00FC,

    /// <summary>
    /// Server responds with Reflector Session Token and Endpoint.
    /// </summary>
    ReflectorAllocated = 0x00FB,
}
