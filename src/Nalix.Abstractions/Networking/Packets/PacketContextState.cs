// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Defines the lifecycle states of a <c>PacketContext&lt;TPacket&gt;</c> instance
/// within the network dispatch system.
/// </summary>
/// <remarks>
/// This enumeration is used internally to track whether a packet context
/// is currently in the object pool, in use by a handler, or has been returned
/// after processing.
/// </remarks>
public enum PacketContextState : byte
{
    /// <summary>
    /// The context is stored in the object pool and available for allocation.
    /// </summary>
    Pooled = 0,

    /// <summary>
    /// The context is actively in use by a packet handler and not available for pooling.
    /// </summary>
    InUse = 1,

    /// <summary>
    /// The context has completed processing and has been returned for reuse.
    /// </summary>
    Returned = 2
}
