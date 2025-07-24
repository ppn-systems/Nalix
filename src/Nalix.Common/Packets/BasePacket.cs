using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;

namespace Nalix.Common.Packets;

/// <summary>
/// Represents the base class for all packet types, providing common properties and serialization methods.
/// </summary>
[SerializePackable(SerializeLayout.Sequential)]
public abstract class BasePacket : IPacket
{
    /// <inheritdoc/>
    public System.UInt16 Length { get; set; }

    /// <inheritdoc/>
    public System.UInt32 MagicNumber { get; set; }

    /// <inheritdoc/>
    public System.UInt16 OpCode { get; set; }

    /// <inheritdoc/>
    public PacketFlags Flags { get; set; }

    /// <inheritdoc/>
    [SerializeIgnore]
    public PacketPriority Priority { get; set; }

    /// <inheritdoc/>
    public TransportProtocol Transport { get; set; }

    /// <inheritdoc/>
    [SerializeIgnore]
    public System.Int32 Hash => MagicNumber.GetHashCode() ^ Transport.GetHashCode();

    /// <summary>
    /// Initializes a new instance of the <see cref="BasePacket"/> class.
    /// </summary>
    protected BasePacket()
    {
        MagicNumber = 0x4E4C5850;

        Length = 0;
        OpCode = 0;
        Flags = PacketFlags.None;
        Priority = PacketPriority.Low;
        Transport = TransportProtocol.Tcp;
    }

    /// <inheritdoc/>
    public abstract System.Byte[] Serialize();

    /// <inheritdoc/>
    public abstract void Serialize(System.Span<System.Byte> buffer);

    /// <inheritdoc/>
    public abstract void ResetForPool();
}