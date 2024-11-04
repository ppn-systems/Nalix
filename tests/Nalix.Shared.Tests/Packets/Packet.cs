using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;

namespace Nalix.Shared.Tests.Packets;

[SerializePackable(SerializeLayout.Sequential)]
public class ConnectionInitPacket : PacketBase
{
    public override System.UInt16 Length { get; set; }

    public override System.UInt16 OpCode { get; set; }

    public override System.UInt32 MagicNumber { get; set; }

    [SerializeDynamicSize(32)]
    public System.Byte[] Payload { get; set; }

    public ConnectionInitPacket()
    {
        Length = 0;
        OpCode = 0;
        Payload = [];
        MagicNumber = 0x12345678; // Example magic number
    }

    public override void ResetForPool()
    {
        Length = 0;
        OpCode = 0;
        Payload = [];
    }
}
