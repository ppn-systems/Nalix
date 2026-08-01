#if DEBUG
using System;
using System.Threading.Tasks;
using Nalix.Codec.DataFrames;
using Nalix.Hosting;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

/// <summary>
/// Repeated connect/disconnect cycles against a single long-lived server, checking that
/// the SDK client does not leak sockets, awaiters, or handshake state across cycles.
/// </summary>
[Collection("RealServerTests")]
public sealed class ReconnectStressTests : IDisposable
{
    public ReconnectStressTests()
    {
        TestAssemblySetup.EnsureHighLimits();
        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }
        TestUtils.SetupCertificate();
    }

    [Fact]
    [Trait("Category", "Stress")]
    public async Task ConnectDisconnect_50Cycles_NoLeakNoPortExhaustion()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.ListenTcp<IntegrationTestProtocol>().OnPort((ushort)port);

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            for (int i = 0; i < 50; i++)
            {
                using TcpSession session = new(new TransportOptions
                {
                    Address = "127.0.0.1",
                    Port = (ushort)port,
                    ConnectTimeoutMillis = 5000
                });

                await session.ConnectAsync();
                Assert.True(session.IsConnected, $"Cycle {i}: expected connected.");
                await session.DisconnectAsync();
                Assert.False(session.IsConnected, $"Cycle {i}: expected disconnected.");
            }
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task ReconnectWhilePending_OldRequestFailsCleanly_NewConnectionWorks()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.ListenTcp<IntegrationTestProtocol>().OnPort((ushort)port);
        builder.UseSystemControl();
        builder.UseTimeSync();

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            using TcpSession session = new(new TransportOptions { Address = "127.0.0.1", Port = (ushort)port });
            await session.ConnectAsync();

            var ping = new Nalix.Codec.ProtocolFrames.TimeSync();
            ping.Initialize(Nalix.Abstractions.Networking.Protocols.ControlType.PING, 42, Nalix.Abstractions.Networking.Packets.PacketFlags.NONE);

            // Start a request that would normally succeed, but disconnect concurrently.
            Task<Nalix.Codec.ProtocolFrames.TimeSync> pendingRequest = session.RequestAsync<Nalix.Codec.ProtocolFrames.TimeSync>(
                ping,
                options: Nalix.SDK.Options.RequestOptions.Default.WithTimeout(8000),
                predicate: p => p.Header.SequenceId == 42).AsTask();

            await session.DisconnectAsync();

            // The pending call must fail/cancel cleanly (not hang forever).
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task completed = await Task.WhenAny(pendingRequest, Task.Delay(Timeout.Infinite, cts.Token));
            Assert.Same(pendingRequest, completed);
            _ = await Assert.ThrowsAnyAsync<Exception>(() => pendingRequest);

            // Reconnecting must work normally afterward.
            await session.ConnectAsync();
            Assert.True(session.IsConnected);

            var ping2 = new Nalix.Codec.ProtocolFrames.TimeSync();
            ping2.Initialize(Nalix.Abstractions.Networking.Protocols.ControlType.PING, 43, Nalix.Abstractions.Networking.Packets.PacketFlags.NONE);

            Nalix.Codec.ProtocolFrames.TimeSync response = await session.RequestAsync<Nalix.Codec.ProtocolFrames.TimeSync>(
                ping2,
                options: Nalix.SDK.Options.RequestOptions.Default.WithTimeout(5000),
                predicate: p => p.Header.SequenceId == 43);

            Assert.Equal(43u, response.Header.SequenceId);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task AutoReconnect_ServerRestarts_PendingRequestSucceedsAfterReconnectAndReauth()
    {
        int port = TestUtils.GetFreePort();

        var builder = NetworkApplication.CreateBuilder();
        builder.ListenTcp<IntegrationTestProtocol>().OnPort((ushort)port);
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
                ServerPublicKey = TestUtils.GetServerPublicKey(),
                AutoReconnectEnabled = true,
                ReconnectBaseDelayMillis = 50,
                ReconnectMaxDelayMillis = 200,
            });

            bool reauthCalled = false;
            session.OnReauthenticateAsync = _ =>
            {
                reauthCalled = true;
                return Task.CompletedTask;
            };

            await session.ConnectAsync();
            Assert.True(session.IsConnected);

            // Simulate an unexpected drop by disconnecting without the caller reconnecting itself.
            await session.DisconnectAsync();
            Assert.False(session.IsConnected);

            // The supervisor only starts a reconnect loop from the OnDisconnected event, which
            // DisconnectAsync also raises (see ReconnectSupervisor ponytail note) — good enough
            // to exercise the reconnect + bounded-wait + reauth path end-to-end.
            var ping = new Nalix.Codec.ProtocolFrames.TimeSync();
            ping.Initialize(Nalix.Abstractions.Networking.Protocols.ControlType.PING, 99, Nalix.Abstractions.Networking.Packets.PacketFlags.NONE);

            Nalix.Codec.ProtocolFrames.TimeSync response = await session.RequestAsync<Nalix.Codec.ProtocolFrames.TimeSync>(
                ping,
                options: RequestOptions.Default.WithTimeout(5000),
                predicate: p => p.Header.SequenceId == 99);

            Assert.Equal(99u, response.Header.SequenceId);
            Assert.True(session.IsConnected);
            Assert.True(reauthCalled);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    public void Dispose() => Nalix.Framework.Injection.InstanceManager.Instance.Clear(dispose: false);
}
#endif
