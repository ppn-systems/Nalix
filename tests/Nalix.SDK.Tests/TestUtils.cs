using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Codec.ProtocolFrames;
using Nalix.Environment.Memory;
using Nalix.Hosting.Protocols;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Handlers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Tests;

internal static class TestUtils
{
    public static void SetupCertificate()
    {
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        string? certPath = null;

        for (int i = 0; i < 10 && current != null; i++)
        {
            string candidate = Path.Combine(current, "shared", "certificate.private");
            if (File.Exists(candidate))
            {
                certPath = candidate;
                break;
            }
            current = Path.GetDirectoryName(current);
        }

        if (certPath == null)
        {
            // Try absolute fallback
            string fallback = @"e:\Cs\Nalix\shared\certificate.private";
            if (File.Exists(fallback)) certPath = fallback;
        }

        if (certPath != null)
        {
            HandshakeHandlers.SetCertificatePath(certPath);
            return;
        }

        HandshakeHandlers.Initialize();
    }

    public static int GetFreePort()
    {
        System.Net.Sockets.TcpListener l = new(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
    public static string GetServerPublicKey()
    {
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        string? pubPath = null;

        for (int i = 0; i < 10 && current != null; i++)
        {
            string candidate = Path.Combine(current, "shared", "certificate.public");
            if (File.Exists(candidate))
            {
                pubPath = candidate;
                break;
            }
            current = Path.GetDirectoryName(current);
        }

        if (pubPath == null)
        {
            string fallback = @"e:\Cs\Nalix\shared\certificate.public";
            if (File.Exists(fallback)) pubPath = fallback;
        }

        if (pubPath != null)
        {
            string[] lines = System.IO.File.ReadAllLines(pubPath);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }
                return trimmed;
            }
        }

        SetupCertificate();
        if (!HandshakeHandlers.ServerPublicKey.IsZero)
        {
            return HandshakeHandlers.ServerPublicKey.ToString();
        }

        throw new System.IO.FileNotFoundException("Public key file not found.");
    }
}

/// <summary>
/// A simple protocol for integration testing that dispatches packets to the Nalix runtime.
/// Copied from NetworkApplicationIntegrationTests to satisfy "real server" requirement in SDK tests.
/// </summary>
public class IntegrationTestProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;
    private readonly DefaultFrameProcessor _frameProcessor;

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
        _frameProcessor = new DefaultFrameProcessor(this);
        this.KeepConnectionOpen = true;
        this.SetConnectionAcceptance(true);
    }

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        if (args.Lease is IBufferLease lease)
        {
            Console.WriteLine($"[TEST] IntegrationTestProtocol.ProcessMessage: Received {lease.Length} bytes.");
            _dispatch.HandlePacket(lease, args.Connection);
        }
    }

    public override void OnAccept(IConnection connection, CancellationToken cancellationToken = default)
    {
        base.OnAccept(connection, cancellationToken);
    }
}

internal sealed class FakeSession(bool isConnected) : TransportSession
{
    private readonly FakePacketRegistry _catalog = new();
    public override TransportOptions Options { get; } = new();
    public override bool IsConnected { get; } = isConnected;
    public int SendPacketCallCount { get; private set; }

    // Required overrides; test code subscribes via SubscribeTemp / On<T> extension methods.
#pragma warning disable CS0067 // Event is never used
    public override event EventHandler? OnConnected;
    public override event EventHandler<Exception>? OnDisconnected;
    public override event EventHandler<IBufferLease>? OnMessageReceived;
    public override event EventHandler<Exception>? OnError;
#pragma warning restore CS0067

    public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default) => Task.CompletedTask;
    public override Task DisconnectAsync() => Task.CompletedTask;

    public override Task SendAsync(IPacket packet, CancellationToken ct = default)
    {
        SendPacketCallCount++;
        if (packet is Control ping && _catalog.TryDequeue(out IPacket? response) && response is Control pong)
        {
            var h = pong.Header;
            h.SequenceId = ping.Header.SequenceId;
            pong.Header = h;

            byte[] data = new byte[PacketConstants.HeaderSize];
            ushort opCode = Control.StaticOpCode;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((int)PacketHeaderOffset.OpCode), opCode);

            using BufferLease lease = BufferLease.CopyFrom(data);
            OnMessageReceived?.Invoke(this, lease);
        }
        return Task.CompletedTask;
    }

    public override Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default) => SendAsync(packet, ct);
    public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default) => Task.CompletedTask;

    public void EnqueueNextPacket(IPacket packet) => _catalog.Enqueue(packet);

    protected override void Dispose(bool disposing) { }
}

internal sealed class FakePacketRegistry : IPacketRegistry
{
    private readonly ConcurrentQueue<IPacket> _queue = new();
    private IPacket? _lastDequeued;

    public int DeserializerCount => 1;
    public bool IsKnownMagic(uint magic) => true;
    public bool IsKnownOpCode(ushort opcode) => true;
    public bool IsRegistered<TPacket>() where TPacket : IPacket => true;
    public void Enqueue(IPacket packet) => _queue.Enqueue(packet);
    public bool TryDequeue(out IPacket? packet)
    {
        bool ok = _queue.TryDequeue(out packet);
        if (ok) _lastDequeued = packet;
        return ok;
    }

    public IPacket Deserialize(ReadOnlySpan<byte> raw) => _lastDequeued ?? new Control();
    public bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
    {
        packet = _lastDequeued ?? new Control();
        return true;
    }
}
















