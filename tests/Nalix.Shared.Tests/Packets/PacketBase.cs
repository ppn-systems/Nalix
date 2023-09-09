using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Tests.Packets;

public abstract class PacketBase : IPacket
{
    // Default implementations (virtual để override khi cần)
    public virtual PacketFlags Flags => PacketFlags.None;
    public virtual PacketPriority Priority => PacketPriority.Low;
    public virtual TransportProtocol Transport => TransportProtocol.Tcp;

    [SerializeIgnore]
    public virtual System.Int32 Hash => 0;

    // Require subclasses to implement
    public abstract System.UInt16 Length { get; set; }
    public abstract System.UInt16 OpCode { get; set; }
    public abstract System.UInt32 MagicNumber { get; set; }

    // Serialization logic (you may move it to a helper if needed)
    public virtual System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    public virtual void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    public virtual void ResetForPool() { }
}
