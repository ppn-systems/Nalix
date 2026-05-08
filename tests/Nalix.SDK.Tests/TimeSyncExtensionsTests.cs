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
public sealed class TimeSyncExtensionsTests : IDisposable
{
    public TimeSyncExtensionsTests()
    {
        PacketRegistry.Build();
        TestUtils.SetupCertificate();
    }

    [Fact]
    public async Task SyncTimeAsync_WhenSuccessful_ReturnsRttAndAdjusted()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.BindTcp<IntegrationTestProtocol>().OnPort((ushort)port);
        
        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            var options = new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                TimeSyncEnabled = true
            };

            using var session = new TcpSession(options);
            await session.ConnectAsync();

            (double rtt, double adjusted) = await session.SyncTimeAsync(timeoutMs: 10000);
            
            Assert.True(rtt >= 0);
            // Adjusted might be 0 if clocks are perfectly synced, but usually it's non-zero
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }
    public void Dispose() => Nalix.Framework.Injection.InstanceManager.Instance.Clear(dispose: false);
}
#endif
















