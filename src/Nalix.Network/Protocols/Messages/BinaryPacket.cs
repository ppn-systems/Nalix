using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Serialization;

namespace Nalix.Network.Protocols.Messages;

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
    [SerializeIgnore]
    public System.UInt16 Length => (System.UInt16)(PacketConstants.HeaderSize + (Data?.Length ?? 0));

    /// <summary>
    /// Gets the magic number used to identify the packet type or protocol.
    /// </summary>
    [SerializeOrder(0)]
    public System.UInt32 MagicNumber { get; set; }

    /// <summary>
    /// Gets the opcode representing the command or category of this packet.
    /// </summary>
    [SerializeOrder(4)]
    public System.UInt16 OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with this packet.
    /// </summary>
    [SerializeOrder(6)]
    public PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the priority level of the packet.
    /// </summary>
    [SerializeOrder(7)]
    public PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the transport protocol (TCP/UDP) this packet is intended for.
    /// </summary>
    [SerializeOrder(8)]
    public TransportProtocol Transport { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(9)]
    [SerializeDynamicSize(512)]
    public System.Byte[] Data { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryPacket"/> class with empty content.
    /// </summary>
    public BinaryPacket()
    {
        OpCode = 0x00;
        MagicNumber = PacketConstants.MagicNumber;

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
        => Initialize(data, TransportProtocol.Null);

    /// <summary>
    /// Initializes the packet with all necessary parameters.
    /// </summary>
    /// <param name="data">Binary content of the packet.</param>
    /// <param name="transport">The transport protocol this packet is intended for.</param>
    public void Initialize(
        System.Byte[] data,
        TransportProtocol transport = TransportProtocol.Tcp)
    {
        Data = data ?? [];
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
        Data = [];

        Flags = PacketFlags.None;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString()
        => $"BinaryPacket(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
           $"Priority={Priority}, Transport={Transport}, Data={Data?.Length ?? 0} bytes)";
}
