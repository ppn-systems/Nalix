using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Transport;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class LiteralPacket : IPacket
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes.
    /// Includes metadata and content.
    /// </summary>
    [SerializeIgnore]
    public System.UInt16 Length => (System.UInt16)(
        PacketConstants.HeaderSize +
        System.Text.Encoding.UTF8.GetByteCount(Content ?? System.String.Empty));

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
    /// Gets or sets the content of the packet as a string.
    /// </summary>
    [SerializeOrder(9)]
    [SerializeDynamicSize(256)]
    public System.String Content { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteralPacket"/> class with empty content.
    /// </summary>
    public LiteralPacket()
    {
        OpCode = 0x00;
        MagicNumber = PacketConstants.MagicNumber;

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
        => Initialize(content, TransportProtocol.Null);

    /// <summary>
    /// Initializes the packet with the specified parameters.
    /// </summary>
    /// <param name="content">The UTF-8 string to be stored in the packet.</param>
    /// <param name="transport">The transport protocol this packet is intended for.</param>
    public void Initialize(
        System.String content,
        TransportProtocol transport = TransportProtocol.Tcp)
    {
        Transport = transport;
        Content = content ?? System.String.Empty;
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
        Flags = PacketFlags.None;
        Content = System.String.Empty;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString()
        => $"BinaryPacket(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
           $"Priority={Priority}, Transport={Transport}, Content={(Content?.Length ?? 0) * 4} bytes)";
}
