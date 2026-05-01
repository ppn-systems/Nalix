#if DEBUG
using System;
using System.Net;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Hosting;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class RequestIntegrationTests : IDisposable
{
    private readonly IPacketRegistry _registry;

    public RequestIntegrationTests()
    {
        _registry = new PacketRegistryFactory()
            .IncludeNamespace("Nalix.Codec.DataFrames.SignalFrames")
            .CreateCatalog();
        TestUtils.SetupCertificate();
    }

    [Fact]
    public async Task RequestAsync_ControlPacket_Succeeds()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.ConfigurePacketRegistry(_registry);
        builder.AddTcp<IntegrationTestProtocol>((ushort)port);
        
        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            var options = new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                EncryptionEnabled = false
            };

            using var session = new TcpSession(options, _registry);
            await session.ConnectAsync();

            // PING expects PONG (which is a Control packet with same Seq)
            var ping = new Control();
            ping.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PING, 1234, PacketFlags.NONE, ProtocolReason.NONE);

            Control response = await session.RequestAsync<Control>(
                ping,
                options: RequestOptions.Default,
                predicate: p => p.Type == ControlType.PONG && p.Header.SequenceId == 1234);

            Assert.Equal(ControlType.PONG, response.Type);
            Assert.Equal(1234u, response.Header.SequenceId);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task RequestAsync_WhenTimeout_ThrowsTimeoutException()
    {
        // Connect to a port that doesn't have a Nalix server
        int port = TestUtils.GetFreePort();
        
        // We don't start the app, so it won't respond
        var options = new TransportOptions
        {
            Address = "127.0.0.1",
            Port = (ushort)port,
            EncryptionEnabled = false
        };

        // Wait! If we don't start the server, ConnectAsync might fail.
        // We need a server that accepts but doesn't respond.
        System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();

        try
        {
            using var session = new TcpSession(options, _registry);
            await session.ConnectAsync();

            var ping = new Control();
            ping.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PING, 1234, PacketFlags.NONE, ProtocolReason.NONE);

            await Assert.ThrowsAsync<TimeoutException>(async () =>
                await session.RequestAsync<Control>(
                    ping,
                    options: RequestOptions.Default.WithTimeout(100).WithRetry(0),
                    predicate: _ => true));
        }
        finally
        {
            listener.Stop();
        }
    }
    public void Dispose() => Nalix.Framework.Injection.InstanceManager.Instance.Clear(dispose: false);
}
#endif















