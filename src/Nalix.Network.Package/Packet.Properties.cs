using Nalix.Common.Constants;
using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Network.Package.Engine.Internal;

namespace Nalix.Network.Package;

[SerializePackable(SerializeLayout.Explicit)]
public readonly partial struct Packet
{
    #region Constants

    // Cache the max packet size locally to avoid field access costs
    private const System.Int32 MaxPacketSize = PacketConstants.PacketSizeLimit;

    private const System.Int32 MaxHeapAllocSize = PacketConstants.HeapAllocLimit;
    private const System.Int32 MaxStackAllocSize = PacketConstants.StackAllocLimit;

    #endregion Constants

    #region Fields

    [SerializeIgnore]
    private readonly System.Int32 _hash;

    [SerializeIgnore]
    private readonly ManagedBuffer _buffer;

    /// <summary>
    /// UTF-8 encoding instance for packet processing.
    /// </summary>
    [SerializeIgnore]
    public static readonly System.Text.Encoding Utf = System.Text.Encoding.UTF8;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Empty packet instance with default values.
    /// </summary>
    [field: SerializeIgnore]
    [SerializeIgnore]
    public static Packet Empty { get; } = new(
        0, 0, 0, 0,
        PacketType.None,
        PacketFlags.None,
        PacketPriority.Low,
        System.ReadOnlyMemory<System.Byte>.Empty);

    /// <summary>
    /// Gets the total length of the packet including header and payload.
    /// </summary>
    [SerializeOrder(PacketOffset.Length)]
    public System.UInt16 Length => (System.UInt16)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the Number associated with the packet, which specifies an operation type.
    /// </summary>
    [SerializeOrder(PacketOffset.OpCode)]
    public System.UInt16 OpCode { get; }

    /// <summary>
    /// Gets the packet identifier, which is a unique identifier for this packet instance.
    /// </summary>
    [SerializeOrder(PacketOffset.Number)]
    public System.Byte Number { get; }

    /// <summary>
    /// Gets the CRC32 checksum of the packet payload for integrity validation.
    /// </summary>
    [SerializeOrder(PacketOffset.Checksum)]
    public System.UInt32 Checksum { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created in microseconds since system startup.
    /// </summary>
    [SerializeOrder(PacketOffset.Timestamp)]
    public System.Int64 Timestamp { get; }

    /// <summary>
    /// Gets the packet Hash.
    /// </summary>
    [field: SerializeIgnore]
    [SerializeIgnore]
    public System.Int32 Hash { get; }

    /// <summary>
    /// Gets the packet type, which specifies the kind of packet.
    /// </summary>
    [SerializeOrder(PacketOffset.Type)]
    public PacketType Type { get; }

    /// <summary>
    /// Gets the flags associated with the packet, used for additional control information.
    /// </summary>
    [SerializeOrder(PacketOffset.Flags)]
    public PacketFlags Flags { get; }

    /// <summary>
    /// Gets the priority level of the packet, which affects how the packet is processed.
    /// </summary>
    [SerializeOrder(PacketOffset.Priority)]
    public PacketPriority Priority { get; }

    /// <summary>
    /// Gets the payload data being transmitted in this packet.
    /// </summary>
    [SerializeOrder(PacketOffset.Payload)]
    public System.ReadOnlyMemory<System.Byte> Payload { get; }

    #endregion Properties
}