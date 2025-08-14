// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Dispatch.Core.Context;

/// <summary>
/// Defines the lifecycle states of a <see cref="PacketContext{TPacket}"/> instance
/// within the network dispatch system.
/// </summary>
/// <remarks>
/// This enumeration is used internally to track whether a packet context
/// is currently in the object pool, in use by a handler, or has been returned
/// after processing.
/// </remarks>
internal enum PacketContextState : System.Byte
{
    /// <summary>
    /// The context is stored in the object pool and available for allocation.
    /// </summary>
    Pooled,

    /// <summary>
    /// The context is actively in use by a packet handler and not available for pooling.
    /// </summary>
    InUse,

    /// <summary>
    /// The context has completed processing and has been returned for reuse.
    /// </summary>
    Returned
}
