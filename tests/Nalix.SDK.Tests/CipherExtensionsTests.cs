#if DEBUG
using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.DataFrames;
using Nalix.Hosting;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Nalix.Framework.Injection;
using Nalix.Codec.ProtocolFrames;
using Nalix.Codec.Pooling;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class CipherExtensionsTests : IDisposable
{
    public CipherExtensionsTests()
    {
        if (!PacketRegistry.IsBuilt)
        PacketRegistry.Build();
        TestUtils.SetupCertificate();
    }

    [Fact]
    public async Task UpdateCipherAsync_WhenSuccessful_SwitchesAlgorithm()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.BindTcp<IntegrationTestProtocol>().OnPort((ushort)port);
        builder.UseSecureConnections();
        builder.UseSystemControl();

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            var options = new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                Algorithm = CipherSuiteType.Chacha20Poly1305,
                ServerPublicKey = TestUtils.GetServerPublicKey()
            };

            using var session = new TcpSession(options);
            await session.ConnectAsync();
            await session.HandshakeAsync();

            await session.UpdateCipherAsync(CipherSuiteType.Salsa20Poly1305, timeoutMs: 20_000);

            Assert.Equal(CipherSuiteType.Salsa20Poly1305, session.Options.Algorithm);

            // Send a ping to verify Salsa20 works for subsequent packets
            using PacketScope<Control> pingLease = PacketFactory<Control>.Acquire();
            var ping = pingLease.Value;
            ping.Initialize((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PING, 100, PacketFlags.NONE, ProtocolReason.NONE);

            using var pong = await session.RequestAsync<Control>(ping);
            Assert.Equal(ControlType.PONG, pong.Type);
            Assert.Equal(ping.Header.SequenceId, pong.Header.SequenceId);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    public void Dispose() => InstanceManager.Instance.Clear(dispose: false);
}
#endif
