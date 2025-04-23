using Nalix.Common.Constants;
using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;
using Nalix.Environment;

namespace Nalix.Network.Package;

public readonly partial struct Packet
{
    #region Constants

    // Cache the max packet size locally to avoid field access costs
    private const int MaxPacketSize = PacketConstants.PacketSizeLimit;

    private const int MaxHeapAllocSize = Constants.HeapAllocThreshold;
    private const int MaxStackAllocSize = Constants.StackAllocThreshold;

    #endregion Constants

    #region Fields

    private readonly ulong _hash;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total length of the packet including header and payload.
    /// </summary>
    public ushort Length => (ushort)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the Number associated with the packet, which specifies an operation type.
    /// </summary>
    public ushort Id { get; }

    /// <summary>
    /// Gets the CRC32 checksum of the packet payload for integrity validation.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created in microseconds since system startup.
    /// </summary>
    public ulong Timestamp { get; }

    /// <summary>
    /// Gets the packet Hash.
    /// </summary>
    public ulong Hash => _hash;

    /// <summary>
    /// Gets the packet code, which is used to identify the packet type.
    /// </summary>
    public PacketCode Code { get; }

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
    /// Gets the packet identifier, which is a unique identifier for this packet instance.
    /// </summary>
    public byte Number { get; }

    /// <summary>
    /// Gets the payload data being transmitted in this packet.
    /// </summary>
    public System.Memory<byte> Payload { get; }

    #endregion Properties
}
