



using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.SDK.Transport;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Registry;



PacketRegistry packetRegistry = new PacketRegistryFactory().CreateCatalog();
InstanceManager.Instance.Register<IPacketRegistry>(packetRegistry);

TcpSession client = InstanceManager.Instance.GetOrCreateInstance<TcpSession>();

Handshake handshake = new(0, Csprng.GetBytes(32))
{
    Flags = PacketFlags.ENCRYPTED | PacketFlags.COMPRESSED
};

await client.ConnectAsync("127.0.0.1", 12345);
Byte[] data = handshake.Serialize();
for (Int32 i = 0; i < 100000; i++)
{
    System.Console.Write($"Sending handshake packet {i + 1}/100000...");
    await client.SendAsync(data);
}

//await Task.Delay(10000); // Wait for response (for demonstration purposes)