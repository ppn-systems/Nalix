using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets.Enums;

namespace Nalix.Common.Packets;

/// <summary>
/// Represents default values and constants for packet configurations.
/// This class contains minimum and maximum packet sizes, as well as thresholds for optimized memory operations.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// The size (in bytes) of the packet header, including flags, OpCode, Length, MagicNumber, priority, and protocol.
    /// </summary>
    public const System.Int32 HeaderSize =
        sizeof(PacketFlags) +
        sizeof(System.UInt16) +  // OpCode
        sizeof(System.UInt16) +  // Length
        sizeof(System.UInt32) +  // MagicNumber
        sizeof(PacketPriority) +
        sizeof(TransportProtocol);

    /// <summary>
    /// The magic number used to identify valid packets.
    /// </summary>
    public const System.Int32 MagicNumber = 0x4E584C58;

    /// <summary>
    /// The threshold size (in bytes) for using heap-based memory allocation.
    /// This value represents the maximum size for which memory should be allocated from the heap instead of the stack.
    /// </summary>
    public const System.Int32 HeapAllocLimit = 0x0400;

    /// <summary>
    /// Maximum stack allocation size (in bytes).
    /// </summary>
    public const System.Int32 StackAllocLimit = 0x0200;

    /// <summary>
    /// The maximum allowed packet size (in bytes), which is 64KB (65535 bytes).
    /// </summary>
    public const System.Int32 PacketSizeLimit = 0xFFFF;

    /// <summary>
    /// The minimum payload size (in bytes) required to trigger compression.
    /// Packets with a payload smaller than this threshold will not be compressed.
    /// </summary>
    public const System.Int32 CompressionThreshold = 0x0100;
}
