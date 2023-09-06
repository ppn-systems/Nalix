using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Serialization;

namespace Nalix.Network.Messages;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class TextPacket : IPacket
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes.
    /// Includes metadata and content.
    /// </summary>
    [SerializeOrder(0)]
    public System.UInt16 Length => (System.UInt16)(
        Content.Length +
        sizeof(PacketFlags) +
        sizeof(System.UInt16) +       // OpCode
        sizeof(System.UInt16) +       // Length
        sizeof(System.UInt32) +       // MagicNumber
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
    /// Gets or sets the content of the packet as a string.
    /// </summary>
    [SerializeOrder(11)]
    [SerializeDynamicSize(256)]
    public System.String Content;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextPacket"/> class with empty content.
    /// </summary>
    public TextPacket()
    {
        OpCode = 0x00;
        MagicNumber = 0x00000000;

        Flags = PacketFlags.None;
        Content = System.String.Empty;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
    }

    /// <summary>
    /// Initializes the packet with the specified string content.
    /// </summary>
    /// <param name="content">The UTF-8 string to be stored in the packet.</param>
    public void Initialize(System.String content)
        => Initialize(content, 0x00, 0x00000000, PacketFlags.None, TransportProtocol.Null);

    /// <summary>
    /// Initializes the packet with the specified parameters.
    /// </summary>
    /// <param name="content">The UTF-8 string to be stored in the packet.</param>
    /// <param name="opCode">The operation code that identifies this packet type.</param>
    /// <param name="magicNumber">A protocol identifier or packet family identifier.</param>
    /// <param name="flags">Optional packet flags (default is None).</param>
    /// <param name="transport">The transport protocol this packet is intended for.</param>
    public void Initialize(
        System.String content,
        System.UInt16 opCode,
        System.UInt32 magicNumber,
        PacketFlags flags = PacketFlags.None,
        TransportProtocol transport = TransportProtocol.Tcp)
    {
        Content = content ?? System.String.Empty;
        OpCode = opCode;
        MagicNumber = magicNumber;
        Flags = flags;
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
    /// <param name="buffer">
    /// The span of bytes where the serialized data will be written.
    /// The buffer must be large enough to hold the entire serialized packet.
    /// </param>
    public void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Resets the packet content to its default state for object pooling reuse.
    /// </summary>
    public void ResetForPool()
    {
        OpCode = 0x00;
        MagicNumber = 0x00000000;

        Flags = PacketFlags.None;
        Content = System.String.Empty;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
    }
}
