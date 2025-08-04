using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Serialization;

namespace Nalix.Network.Messages;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class BinaryPacket : IPacket
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes.
    /// Includes metadata and content.
    /// </summary>
    [SerializeOrder(0)]
    public System.UInt16 Length => (System.UInt16)(
        (Data?.Length ?? 0) +
        sizeof(PacketFlags) +
        sizeof(System.UInt16) +       // OpCode
        sizeof(System.UInt16) +       // Length
        sizeof(System.UInt32) +         // MagicNumber
        sizeof(PacketPriority) +
        sizeof(TransportProtocol));

    /// <summary>
    /// Gets the magic number used to identify the packet type or protocol.
    /// </summary>
    [SerializeOrder(2)]
    public System.UInt32 MagicNumber { get; set; }

    /// <summary>
    /// Gets the opcode representing the command or category of this packet.
    /// </summary>
    [SerializeOrder(6)]
    public System.UInt16 OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with this packet.
    /// </summary>
    [SerializeOrder(8)]
    public PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the priority level of the packet.
    /// </summary>
    [SerializeOrder(9)]
    public PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the transport protocol (TCP/UDP) this packet is intended for.
    /// </summary>
    [SerializeOrder(10)]
    public TransportProtocol Transport { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(11)]
    [SerializeDynamicSize(512)]
    public System.Byte[] Data;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryPacket"/> class with empty content.
    /// </summary>
    public BinaryPacket()
    {
        OpCode = 0x00;
        MagicNumber = 0x00000000;

        Flags = PacketFlags.None;
        Data = [];
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
    }

    /// <summary>
    /// Initializes the packet with binary data.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    public void Initialize(System.Byte[] data)
        => Initialize(data, 0x00, 0x00000000, PacketFlags.None, TransportProtocol.Null);

    /// <summary>
    /// Initializes the packet with all necessary parameters.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    /// <param name="opCode">The operation code that identifies this packet type.</param>
    /// <param name="magicNumber">A protocol identifier or packet family identifier.</param>
    /// <param name="flags">Optional packet flags (default is None).</param>
    /// <param name="transport">The transport protocol this packet is intended for.</param>
    public void Initialize(
        System.Byte[] data,
        System.UInt16 opCode,
        System.UInt32 magicNumber,
        PacketFlags flags = PacketFlags.None,
        TransportProtocol transport = TransportProtocol.Tcp)
    {
        Data = data ?? [];
        OpCode = opCode;
        MagicNumber = magicNumber;
        Flags = flags;
        Priority = PacketPriority.Normal;
        Transport = transport;
    }

    /// <summary>
    /// Serializes the packet into a byte array using the configured serializer.
    /// </summary>
    /// <returns>A byte array containing the serialized representation of the packet.</returns>
    public System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <summary>
    /// Serializes the packet into the provided buffer span.
    /// </summary>
    /// <param name="buffer">The buffer to serialize data into.</param>
    public void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Resets the packet content to its default state for object pooling reuse.
    /// </summary>
    public void ResetForPool()
    {
        OpCode = 0x00;
        MagicNumber = 0x00000000;

        Flags = PacketFlags.None;
        Data = [];
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
    }
}
