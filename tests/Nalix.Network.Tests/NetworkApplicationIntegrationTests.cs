using System.Net;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Hosting;
using Nalix.Hosting.Protocols;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.Network.Tests;

public sealed class NetworkApplicationIntegrationTests
{
    [Fact]
    public void Serialization_Byte_Verification()
    {
        // 1. Create packet
        using var pkt = new HostingScan.HostingScanAttributedPacket { Value = 42 };
        var h = pkt.Header;
        h.OpCode = 0x1234;
        pkt.Header = h;

        // 2. Serialize
        byte[] bytes = pkt.Serialize();

        // 3. Verify
        // Offset 0: OpCode (2 bytes)
        ushort readOpCode = BitConverter.ToUInt16(bytes, 0);

        Console.WriteLine($"[TEST] Serialized bytes: {BitConverter.ToString(bytes)}");
        Console.WriteLine($"[TEST] Expected OpCode: 0x{pkt.Header.OpCode:X4}, Read: 0x{readOpCode:X4}");

        Assert.Equal(pkt.Header.OpCode, readOpCode);


    }

    [Fact]
    public async Task NetworkApplication_LifecycleWithTcpConnection_Succeeds()
    {
        IntegrationTestController.ReceivedCount = 0;
        IntegrationTestProtocol.ProcessedCount = 0;

        // 0. Resolve certificate path (using the sample key provided in shared)
        string certPath = Path.GetFullPath(@"..\..\..\shared\certificate.private");
        if (!File.Exists(certPath))
        {
            certPath = Path.GetFullPath(@"shared\certificate.private");
        }

        if (!File.Exists(certPath))
        {
            certPath = Path.GetFullPath("test_certificate.private");
            File.WriteAllText(certPath, new string('a', 64));
        }

        // Find a random available port
        int port = GetFreePort();

        // 1. Setup Server
        using var logger = new Nalix.Logging.NLogixBuilder()
            .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace)
            .AddTarget(new Nalix.Logging.Sinks.BatchConsoleLogTarget())
            .Build();

        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        builder.UseLogger(logger);
        builder.UseSecureConnections(certPath);

        // Listen on loopback with our test protocol
        builder.ListenTcp<IntegrationTestProtocol>().OnPort((ushort)port);

        builder.MapHandlers<IntegrationTestController>();

        using NetworkApplication app = builder.Build();

        // Start Server
        await app.ActivateAsync();

        try
        {
            // 2. Setup Client
            var options = new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                CompressionEnabled = false
            };
            using TcpSession client = new(options);

            await client.ConnectAsync();

            // 3. Send Packet
            using HostingScan.HostingScanAttributedPacket pkt = new() { Value = 42 };
            var h2 = pkt.Header;
            h2.OpCode = 0x1234;
            pkt.Header = h2;

#if DEBUG
            Console.WriteLine($"[TEST] Sending packet: OpCode={pkt.Header.OpCode}");
#endif
            // Disambiguate SendAsync by specifying CancellationToken
            await client.SendAsync(pkt, ct: default);

            // 4. Verify (with polling for processing)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                while (IntegrationTestProtocol.ProcessedCount < 1 && !cts.IsCancellationRequested)
                {
                    await Task.Delay(100, cts.Token);
                }
            }
            catch (TaskCanceledException) { }

            Assert.Equal(1, IntegrationTestProtocol.ProcessedCount);

            await client.DisconnectAsync();
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    private static int GetFreePort()
    {
        System.Net.Sockets.TcpListener l = new(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}

/// <summary>
/// A simple protocol for integration testing that dispatches packets to the Nalix runtime.
/// </summary>
[Nalix.Abstractions.Injection.Injectable]
public sealed class IntegrationTestProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;
    private readonly DefaultFrameProcessor _frameProcessor;

    public static int ProcessedCount;

    private sealed class StubOpCodeExtractor : Nalix.Abstractions.Networking.Protocols.IOpCodeExtractor
    {
        public ushort Extract(ReadOnlySpan<byte> payload) =>
            payload.Length >= 2 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[0..]) : (ushort)0;
    }

    public override IFrameProcessor FrameProcessor => _frameProcessor;
    public override Nalix.Abstractions.Networking.Protocols.IOpCodeExtractor OpCodeExtractor { get; } = new StubOpCodeExtractor();

    public IntegrationTestProtocol(IPacketDispatch dispatch)
    {
        _dispatch = dispatch;
        _frameProcessor = new DefaultFrameProcessor(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, this);
        this.KeepConnectionOpen = true;
        this.SetConnectionAcceptance(true);
    }

    public override void ProcessMessage(object? sender, IConnectionEventArgs args)
    {
        // For testing, we assume the payload is a valid packet frame
        // and we push it to the dispatcher.
        if (args.Lease is IBufferLease lease)
        {
            Interlocked.Increment(ref ProcessedCount);
            _dispatch.HandlePacket(lease, args.Connection);
        }
    }

    public override void OnAccept(IConnection connection, CancellationToken cancellationToken = default)
    {
        base.OnAccept(connection, cancellationToken);
    }
}

[PacketHandler("IntegrationTest")]
[Nalix.Abstractions.Injection.Injectable]
public sealed class IntegrationTestController
{
    public static int ReceivedCount = 0;

    [PacketOpcode(0x1234)]
    public void Handle(IPacketContext<HostingScan.HostingScanAttributedPacket> context)
    {
        Interlocked.Increment(ref ReceivedCount);
    }
}
















