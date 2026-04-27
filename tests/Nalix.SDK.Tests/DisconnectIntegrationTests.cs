#if DEBUG
using System;
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
public sealed class DisconnectIntegrationTests : IDisposable
{
    private readonly IPacketRegistry _registry;

    public DisconnectIntegrationTests()
    {
        _registry = new PacketRegistryFactory()
            .IncludeNamespace("Nalix.Codec.DataFrames.SignalFrames")
            .CreateCatalog();
        TestUtils.SetupCertificate();
    }

    [Fact]
    public async Task DisconnectGracefullyAsync_Succeeds()
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

            Assert.True(session.IsConnected);

            // 3. Graceful Disconnect
            await session.DisconnectGracefullyAsync(ProtocolReason.SERVER_SHUTDOWN);

            // 4. Verify
            Assert.False(session.IsConnected);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }
    public void Dispose() => Nalix.Framework.Injection.InstanceManager.Instance.Clear(dispose: false);
}
#endif















