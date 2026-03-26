#if DEBUG
using System;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Network.Hosting;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class PingExtensionsTests : IDisposable
{
    private readonly IPacketRegistry _registry;

    public PingExtensionsTests()
    {
        _registry = new PacketRegistryFactory().CreateCatalog();
    }

    [Fact]
    public async Task PingAsync_WithRealServer_ReturnsPositiveRtt()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.ConfigurePacketRegistry(_registry);
        builder.AddTcp<IntegrationTestProtocol>((ushort)port);
        
        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            using var session = new TcpSession(new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port
            }, _registry);

            await session.ConnectAsync();

            double rtt = await session.PingAsync(timeoutMs: 2000);
            
            Assert.True(rtt >= 0, $"RTT should be positive, got {rtt}");
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }
    public void Dispose() => Nalix.Framework.Injection.InstanceManager.Instance.Clear(dispose: false);
}
#endif
