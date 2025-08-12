namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Represents the lifecycle state of a packet in the network dispatch system.
/// </summary>
internal enum PacketContextState : System.Byte
{
    /// <summary>
    /// Indicates that the packet is in the pool, available for allocation.
    /// </summary>
    Pooled,

    /// <summary>
    /// Indicates that the packet is currently in use and not available for allocation.
    /// </summary>
    InUse,

    Returned
}