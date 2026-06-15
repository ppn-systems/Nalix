using System.Net.Sockets;
using System.Net;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.Framework.Injection;
using Nalix.Hosting;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using System.Threading.Tasks;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class UdpIntegrityTests : IDisposable
{
    private readonly Bytes32 _serverPublicKey;

    public UdpIntegrityTests()
    {
        TestAssemblySetup.EnsureHighLimits();
        if (!PacketRegistry.IsBuilt)
            PacketRegistry.Build();
        TestUtils.SetupCertificate();
        _serverPublicKey = Bytes32.Parse(TestUtils.GetServerPublicKey());
    }

    [Fact]
    public async Task UdpSpoofing_WithInvalidXxHash32_IsDropped_WithoutDeductingTrust()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.BindTcp<IntegrationTestProtocol>().OnPort((ushort)port);
        builder.BindUdp<IntegrationTestProtocol>().OnPort((ushort)port);
        builder.UseSecureConnections();
        builder.UseSystemControl();
        builder.UseTimeSync();

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            using TcpSession session = new(new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                ServerPublicKey = _serverPublicKey.ToString()
            });

            await session.ConnectAsync();
            await session.HandshakeAsync();

            Assert.True(session.State.EncryptionEnabled);

            // Now simulate an attacker spoofing UDP packets
            // Attacker knows the SessionToken (which is visible in plaintext UDP headers)
            // But attacker does NOT know the Connection.Secret.
            using UdpClient attacker = new();
            
            // Craft a fake datagram: [SessionToken (8)] + [Payload (N)] + [FakeHash (4)]
            byte[] fakePacket = new byte[8 + 10 + 4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(fakePacket.AsSpan(0, 8), session.State.SessionToken);
            
            // Attacker sends 20 spoofed packets
            for (int i = 0; i < 20; i++)
            {
                await attacker.SendAsync(fakePacket, fakePacket.Length, "127.0.0.1", port);
            }

            // Give the server time to process
            await Task.Delay(500);

            // If the XxHash32 drop logic failed, the server would have processed these as invalid encrypted frames,
            // resulting in Trust Score deduction, and after 10 errors, it would kick the legitimate client!
            // Let's verify the legitimate client is STILL CONNECTED by sending a ping.
            var pingResult = await session.PingAsync();
            Assert.True(pingResult >= 0);
            Assert.True(session.IsConnected);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    public void Dispose() => InstanceManager.Instance.Clear(dispose: false);
}
