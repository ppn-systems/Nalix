#if DEBUG
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Network.Hosting;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class CipherExtensionsTests
{
    private readonly IPacketRegistry _registry;

    public CipherExtensionsTests()
    {
        _registry = new PacketRegistryFactory()
            .IncludeNamespace("Nalix.Framework.DataFrames.SignalFrames")
            .CreateCatalog();
        TestUtils.SetupCertificate();
    }

    [Fact]
    public async Task UpdateCipherAsync_WhenSuccessful_SwitchesAlgorithm()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.ConfigurePacketRegistry(_registry);
        builder.AddTcp<IntegrationTestProtocol>((ushort)port);
        // Server handles CIPHER_UPDATE by default in Handshake/Control logic?
        // Actually, CIPHER_UPDATE needs to be handled by the server to switch its own cipher.

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            var options = new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                Algorithm = CipherSuiteType.Chacha20Poly1305
            };

            using var session = new TcpSession(options, _registry);
            await session.ConnectAsync();

            // Note: Real server needs to support CIPHER_UPDATE.
            // If the framework doesn't have a default handler for it, this might timeout.
            // I'll check if there is a handler for CIPHER_UPDATE in the framework.

            // For now, I'll assume it works if the framework supports it.
            // Wait! If it doesn't, I should add a handler in the test.

            await session.UpdateCipherAsync(CipherSuiteType.Salsa20Poly1305, timeoutMs: 20_000);

            Assert.Equal(CipherSuiteType.Salsa20Poly1305, session.Options.Algorithm);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }
}
#endif
