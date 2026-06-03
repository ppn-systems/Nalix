// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Nalix.Traversal.Protocols;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
    /// Client requests Server to allocate a Reflector Session.
    /// </summary>
    ReflectorInit = 0x00FE,
}
