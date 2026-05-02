using Nalix.Codec.Memory;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Hosting;
using Nalix.Network.Protocols;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.Runtime.Dispatching;
using Nalix.Framework.Extensions;
using Xunit;

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
        // Offset 0: MagicNumber (4 bytes)
        // Offset 4: OpCode (2 bytes)
        ushort readOpCode = BitConverter.ToUInt16(bytes, 4);
        
        Console.WriteLine($"[TEST] Serialized bytes: {BitConverter.ToString(bytes)}");
        Console.WriteLine($"[TEST] Expected OpCode: 0x{pkt.Header.OpCode:X4}, Read: 0x{readOpCode:X4}");
        
        Assert.Equal(pkt.Header.OpCode, readOpCode);
        
        uint magic = BitConverter.ToUInt32(bytes, 0);
        Assert.Equal(pkt.Header.MagicNumber, magic);
    }

    [Fact]
    public async Task NetworkApplication_LifecycleWithTcpConnection_Succeeds()
    {
        IntegrationTestController.ReceivedCount = 0;
        
        // 0. Setup Handshake Certificate (using the sample key provided in shared)
        string certPath = Path.GetFullPath(@"..\..\..\shared\certificate.private");
        if (!File.Exists(certPath))
        {
            // Fallback for different build environments
            certPath = Path.GetFullPath(@"shared\certificate.private");
        }
        
        if (File.Exists(certPath))
        {
             Nalix.Runtime.Handlers.HandshakeHandlers.SetCertificatePath(certPath);
        }
        else
        {
            // Last resort: create dummy if shared is not found (should not happen based on user info)
            certPath = Path.GetFullPath("test_certificate.private");
            File.WriteAllText(certPath, new string('a', 64));
            Nalix.Runtime.Handlers.HandshakeHandlers.SetCertificatePath(certPath);
        }

        // Find a random available port
        int port = GetFreePort();
        
        // 1. Setup Server
        using var logger = new Nalix.Logging.NLogix(opt => 
        {
            opt.MinLevel = Microsoft.Extensions.Logging.LogLevel.Trace;
        });

        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        builder.ConfigureLogging(logger);
        
        // Listen on loopback with our test protocol
        builder.AddTcp<IntegrationTestProtocol>((ushort)port);
        
        // Add current assembly for scanning controllers and packets
        builder.AddPacketNamespace("Nalix.Network.Tests", recursive: true);
        builder.AddHandlers<NetworkApplicationIntegrationTests>();
        
        using NetworkApplication app = builder.Build();
        
        // Start Server
        await app.ActivateAsync();
        
        try 
        {
            // 2. Setup Client
            IPacketRegistry registry = new PacketRegistryFactory()
                .RegisterAllPackets(typeof(NetworkApplicationIntegrationTests).Assembly)
                .CreateCatalog();
            using TcpSession client = new(new TransportOptions 
            { 
                Address = "127.0.0.1", 
                Port = (ushort)port,
                EncryptionEnabled = false 
            }, registry);
            
            await client.ConnectAsync();
            
            // 3. Send Packet
            using HostingScan.HostingScanAttributedPacket pkt = new() { Value = 42 };
            var h2 = pkt.Header;
            h2.OpCode = 0x1234;
            pkt.Header = h2;
            
            #if DEBUG
            Console.WriteLine($"[TEST] Sending packet: OpCode={pkt.Header.OpCode}, MagicNumber={pkt.Header.MagicNumber}");
            #endif
            // Disambiguate SendAsync by specifying CancellationToken
            await client.SendAsync(pkt, ct: default);
            
            // 4. Verify (with polling for processing)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                while (IntegrationTestController.ReceivedCount < 1 && !cts.IsCancellationRequested)
                {
                    await Task.Delay(100, cts.Token);
                }
            }
            catch (TaskCanceledException) { }
            
            Assert.Equal(1, IntegrationTestController.ReceivedCount);
            
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
public sealed class IntegrationTestProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    public IntegrationTestProtocol(IPacketDispatch dispatch)
    {
        _dispatch = dispatch;
        this.SetConnectionAcceptance(true);
    }

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        // For testing, we assume the payload is a valid packet frame
        // and we push it to the dispatcher.
        if (args.Lease is IBufferLease lease)
        {
            _dispatch.HandlePacket(lease, args.Connection);
        }
    }

    public override void OnAccept(IConnection connection, CancellationToken cancellationToken = default)
    {
        base.OnAccept(connection, cancellationToken);
    }
}

[PacketController("IntegrationTest")]
public sealed class IntegrationTestController
{
    public static int ReceivedCount = 0;

    [PacketOpcode(0x1234)]
    public void Handle(IPacketContext<HostingScan.HostingScanAttributedPacket> context)
    {
        Interlocked.Increment(ref ReceivedCount);
    }
}














