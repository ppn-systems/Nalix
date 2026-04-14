using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Network.Hosting;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;
using Nalix.SDK.Extensions;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
[Collection(AsyncCallbackSerialGroup.Name)]
public sealed class SdkExtensionIntegrationTests
{
    [Fact]
    public async Task FullClientServerFlow_CoversCoreSdkExtensions()
    {
        EnsureInfrastructure();

        using TestNetworkHost host = await TestNetworkHost.StartAsync();
        using TestClient client = new(host.Port);

        await client.Session.ConnectAsync();
        await client.Session.HandshakeAsync();

        client.Session.Options.EncryptionEnabled.Should().BeTrue();
        client.Session.Options.Algorithm.Should().Be(CipherSuiteType.Chacha20Poly1305);
        client.Session.Options.SessionToken.Should().NotBe(Snowflake.Empty);

        EchoPacket echo = new()
        {
            Message = "xin chao",
            Counter = 7,
            SequenceId = 11
        };

        EchoPacket echoed = await client.Session.RequestAsync<EchoPacket>(
            echo,
            RequestOptions.Default.WithTimeout(2500),
            predicate: response => response.Message == echo.Message && response.Counter == echo.Counter);

        echoed.Message.Should().Be(echo.Message);
        echoed.Counter.Should().Be(echo.Counter);
        echoed.ReplyCount.Should().Be(1);

        double pingMs = await client.Session.PingAsync(timeoutMs: 2500);
        pingMs.Should().BeGreaterThanOrEqualTo(0);

        (double rttMs, double adjustedMs) = await client.Session.SyncTimeAsync(timeoutMs: 2500);
        rttMs.Should().BeGreaterThanOrEqualTo(0);
        adjustedMs.Should().NotBe(0);

        await client.Session.UpdateCipherAsync(CipherSuiteType.XChaCha20Poly1305, timeoutMs: 2500);
        client.Session.Options.Algorithm.Should().Be(CipherSuiteType.XChaCha20Poly1305);

        await client.Session.DisconnectAsync();
        bool resumed = await client.Session.ConnectWithResumeAsync();
        resumed.Should().BeTrue();
        client.Session.Options.EncryptionEnabled.Should().BeTrue();

        await client.Session.DisconnectGracefullyAsync(reason: ProtocolReason.CLIENT_QUIT);
        client.Session.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Subscriptions_IgnoreNonMatchingPackets_AndDisposeCleanly()
    {
        EnsureInfrastructure();

        using TestNetworkHost host = await TestNetworkHost.StartAsync();
        using TestClient client = new(host.Port);

        await client.Session.ConnectAsync();
        await client.Session.HandshakeAsync();

        TaskCompletionSource<EchoPacket> typedObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<EchoPacket> exactObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Control> filteredObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Exception> disconnectedObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable typedSub = client.Session.On<EchoPacket>(packet =>
        {
            _ = typedObserved.TrySetResult(packet);
        });

        using IDisposable exactSub = client.Session.OnExact<EchoPacket>(packet =>
        {
            _ = exactObserved.TrySetResult(packet);
        });

        using IDisposable filteredSub = client.Session.On(
            packet => packet is Control ctrl && ctrl.Type == ControlType.NOTICE,
            packet => _ = filteredObserved.TrySetResult((Control)packet));

        using IDisposable tempSub = client.Session.SubscribeTemp<EchoPacket>(
            onMessage: packet => _ = exactObserved.TrySetResult(packet),
            onDisconnected: ex => _ = disconnectedObserved.TrySetResult(ex));

        EchoPacket response = await client.Session.RequestAsync<EchoPacket>(
            new EchoPacket { Message = "sub", Counter = 1 },
            RequestOptions.Default.WithTimeout(2500));

        (await typedObserved.Task.WaitAsync(TimeSpan.FromSeconds(5))).Message.Should().Be("sub");
        (await exactObserved.Task.WaitAsync(TimeSpan.FromSeconds(5))).Counter.Should().Be(1);
        response.Message.Should().Be("sub");

        await client.Session.SendControlAsync(
            opCode: (ushort)ProtocolOpCode.SYSTEM_CONTROL,
            type: ControlType.NOTICE,
            configure: ctrl => ctrl.Reason = ProtocolReason.NONE);

        (await filteredObserved.Task.WaitAsync(TimeSpan.FromSeconds(5))).Type.Should().Be(ControlType.NOTICE);

        await client.Session.DisconnectGracefullyAsync(reason: ProtocolReason.CLIENT_QUIT);
        (await disconnectedObserved.Task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeOfType<InvalidOperationException>();
    }

    private static void EnsureInfrastructure()
    {
        _ = InstanceManager.Instance.WithLogging(NullLogger.Instance);
        InstanceManager.Instance.Register<ILogger>(NullLogger.Instance);
    }

    [PacketController]
    private sealed class TestEchoController
    {
        [PacketOpcode(0x1401)]
        public static EchoPacket Handle(EchoPacket packet, Nalix.Common.Networking.IConnection connection)
        {
            _ = connection;
            return new EchoPacket
            {
                Message = packet.Message,
                Counter = packet.Counter,
                ReplyCount = packet.ReplyCount + 1,
                SequenceId = packet.SequenceId
            };
        }
    }

    [SerializePackable(SerializeLayout.Sequential)]
    public sealed class EchoPacket : PacketBase<EchoPacket>
    {
        private const ushort OpCodeValue = 0x1401;

        public EchoPacket()
        {
            this.OpCode = OpCodeValue;
        }

        [SerializeOrder(0)]
        public string Message { get; set; } = string.Empty;

        [SerializeOrder(1)]
        public int Counter { get; set; }

        [SerializeOrder(2)]
        public int ReplyCount { get; set; }

        public static new EchoPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<EchoPacket>.Deserialize(buffer);

        public override void ResetForPool()
        {
            base.ResetForPool();
            this.OpCode = OpCodeValue;
        }
    }

    private sealed class TestNetworkHost : IDisposable
    {
        private readonly NetworkApplication _app;

        private TestNetworkHost(NetworkApplication app, ushort port)
        {
            _app = app;
            Port = port;
        }

        public ushort Port { get; }

        public static async Task<TestNetworkHost> StartAsync()
        {
            ushort port = GetFreePort();

            NetworkApplication app = NetworkApplication.CreateBuilder()
                .AddPacket(typeof(EchoPacket).Assembly)
                .AddHandlers(typeof(TestEchoController).Assembly)
                .AddTcp<TestProtocol>(port)
                .Build();

            await app.ActivateAsync();
            return new TestNetworkHost(app, port);
        }

        public void Dispose()
        {
            try { _app.DeactivateAsync(CancellationToken.None).GetAwaiter().GetResult(); } catch { }
            _app.Dispose();
        }

        private static ushort GetFreePort()
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            return (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }

    private sealed class TestClient : IDisposable
    {
        public TestClient(ushort port)
        {
            PacketRegistryFactory factory = new();
            _ = factory.RegisterPacket<EchoPacket>();

            Session = new TcpSession(
                new TransportOptions
                {
                    Address = "127.0.0.1",
                    Port = port,
                    ResumeEnabled = true,
                    ResumeFallbackToHandshake = true,
                    ResumeTimeoutMillis = 2500
                },
                factory.CreateCatalog());
        }

        public TcpSession Session { get; }

        public void Dispose() => Session.Dispose();
    }

    private sealed class TestProtocol(IPacketDispatch dispatch) : Protocol
    {
        public override void ProcessMessage(object? sender, Nalix.Common.Networking.IConnectEventArgs args)
            => dispatch.HandlePacket(args.Lease!, args.Connection);
    }
}
