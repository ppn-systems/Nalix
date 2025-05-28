using Nalix.Common.Constants;
using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;

namespace Nalix.Network.Package;

public readonly partial struct Packet
{
    #region Constants

    // Cache the max packet size locally to avoid field access costs
    private const int MaxPacketSize = PacketConstants.PacketSizeLimit;

    private const int MaxHeapAllocSize = PacketConstants.HeapAllocLimit;
    private const int MaxStackAllocSize = PacketConstants.StackAllocLimit;

    #endregion Constants

    #region Fields

    private static readonly Packet _empty = new(
        0,
        PacketType.None,
        PacketFlags.None,
        PacketPriority.Low,
        System.Memory<byte>.Empty);

    private readonly ulong _hash;
    private readonly byte[]? _rentedBuffer;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Empty packet instance with default values.
    /// </summary>
    public static Packet Empty => _empty;

    /// <summary>
    /// Gets the total length of the packet including header and payload.
    /// </summary>
    public ushort Length => (ushort)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the Number associated with the packet, which specifies an operation type.
    /// </summary>
    public ushort OpCode { get; }

    /// <summary>
    /// Gets the packet identifier, which is a unique identifier for this packet instance.
    /// </summary>
    public byte Number { get; }

    /// <summary>
    /// Gets the CRC32 checksum of the packet payload for integrity validation.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created in microseconds since system startup.
    /// </summary>
    public long Timestamp { get; }

    /// <summary>
    /// Gets the packet Hash.
    /// </summary>
    public ulong Hash => _hash;

    /// <summary>
    /// Gets the packet type, which specifies the kind of packet.
    /// </summary>
    public PacketType Type { get; }

    /// <summary>
    /// Gets the flags associated with the packet, used for additional control information.
    /// </summary>
    public PacketFlags Flags { get; }

    /// <summary>
    /// Gets the priority level of the packet, which affects how the packet is processed.
    /// </summary>
    public PacketPriority Priority { get; }

    /// <summary>
    /// Gets the payload data being transmitted in this packet.
    /// </summary>
    public System.Memory<byte> Payload { get; }

    #endregion Properties
}
