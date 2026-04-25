#if DEBUG
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Network.Hosting;
using Nalix.Network.Connections;
using Nalix.SDK.Transport;
using Nalix.SDK.Options;
using System.Buffers.Binary;
using Nalix.Framework.DataFrames;

namespace Nalix.SDK.Tests;

[Packet]
public sealed class SpecificUdpIntegrationTestPacket : PacketBase<SpecificUdpIntegrationTestPacket>
{
    public SpecificUdpIntegrationTestPacket()
    {
        this.OpCode = 0x9999;
    }
}

[Collection("RealServerTests")]
public class UdpSessionIntegrationTests
{
    private readonly IPacketRegistry _registry;

    public UdpSessionIntegrationTests()
    {
        // Use factory to create registry that includes our test packet
        _registry = new PacketRegistryFactory()
            .RegisterPacket<SpecificUdpIntegrationTestPacket>()
            .CreateCatalog();
        TestUtils.SetupCertificate();
    }

    [Fact]
    public async Task UdpSession_SendAndReceive_DispatchesToRuntime()
    {
        int port = TestUtils.GetFreePort();
        int receivedCount = 0;

        // 1. Setup Server
        var builder = NetworkApplication.CreateBuilder();
        builder.ConfigurePacketRegistry(_registry);
        builder.AddUdp<IntegrationTestProtocol>((ushort)port);
        // Inject a way to track receipt
        Action<SpecificUdpIntegrationTestPacket> onReceived = pkt => { receivedCount++; };
        builder.AddHandler<UdpTestController>(() => new UdpTestController(onReceived));
        InstanceManager.Instance.Register<Action<SpecificUdpIntegrationTestPacket>>(onReceived);

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            // 2. Mock a connection in the Hub so UdpListener accepts the datagram
            IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()!;
            
            using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            
            Task<Socket> acceptTask = listener.AcceptAsync();
            using Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(listener.LocalEndPoint!);
            using Socket serverSocket = await acceptTask;

            using Connection connection = new(serverSocket);
            Snowflake actualToken = (Snowflake)connection.ID;

            // Fix SEC-30 pinning: overwrite NetworkEndpoint with one that has no port
            var field = typeof(Connection).GetField("<NetworkEndpoint>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(connection, new FakeEndpoint("127.0.0.1", 0));
            
            hub.RegisterConnection(connection);

            // 3. Setup Client
            using UdpSession clientSession = new(new TransportOptions 
            { 
                Address = "127.0.0.1", 
                Port = (ushort)port,
                SessionToken = actualToken,
                EncryptionEnabled = false,
                Secret = new Bytes32(new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 })
            }, _registry);

            await clientSession.ConnectAsync();

            // 4. Send Packet
            SpecificUdpIntegrationTestPacket pkt = new();
            await clientSession.SendAsync(pkt, encrypt: false);

            // 5. Verify
            await Task.Delay(500);
            Assert.Equal(1, receivedCount);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    private sealed class FakeEndpoint(string address, int port) : INetworkEndpoint
    {
        public string Address => address;
        public int Port => port;
        public bool HasPort => port > 0;
        public bool IsLocal => true;
        public bool IsIPv6 => false;
    }

    [PacketController("UdpTest")]
    public sealed class UdpTestController
    {
        private readonly Action<SpecificUdpIntegrationTestPacket> _onReceived;
        public UdpTestController(Action<SpecificUdpIntegrationTestPacket> onReceived) => _onReceived = onReceived;

        [PacketOpcode(0x9999)]
        public void Handle(IPacketContext<SpecificUdpIntegrationTestPacket> context)
        {
            _onReceived(context.Packet);
        }
    }
}
#endif
