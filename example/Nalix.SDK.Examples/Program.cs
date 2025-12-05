



using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.Logging;
using Nalix.SDK.Transport;
using Nalix.Shared.Frames;
using Nalix.Shared.Frames.Controls;

InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
PacketRegistry packetRegistry = new PacketRegistryFactory().CreateCatalog();
InstanceManager.Instance.Register<IPacketRegistry>(packetRegistry);

//TcpSession client = InstanceManager.Instance.GetOrCreateInstance<TcpSession>();

Handshake handshake = new(0, Csprng.GetBytes(32));
Control controlPacket = new();
handshake.ResetForPool();
Thread.Sleep(1000); // Simulate some delay before connecting
Console.WriteLine(controlPacket.MagicNumber.ToString("X8"));
Console.WriteLine(handshake.MagicNumber.ToString("X8"));
System.Console.WriteLine($"Handshake Magic: {handshake.MagicNumber:X8}");

TcpSession client = new();
await client.ConnectAsync("127.0.0.1", 12345);
System.Console.WriteLine(handshake.GenerateReport());
System.Console.WriteLine($"Handshake Magic: {handshake.MagicNumber:X8}");
Byte[] data = handshake.Serialize();
await client.SendAsync(data);
//for (Int32 i = 0; i < 100000; i++)
//{
//    System.Console.Write($"Sending handshake packet {i + 1}/100000...");
//    await client.SendAsync(data);
//}

await Task.Delay(10000); // Wait for response (for demonstration purposes)