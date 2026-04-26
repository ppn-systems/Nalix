using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Tests;

internal static class TestUtils
{
    public static void SetupCertificate()
    {
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        string? certPath = null;

        while (current != null)
        {
            string candidate = System.IO.Path.Combine(current, "shared", "certificate.private");
            if (System.IO.File.Exists(candidate))
            {
                certPath = candidate;
                break;
            }
            current = System.IO.Path.GetDirectoryName(current);
        }

        if (certPath != null)
        {
            Nalix.Runtime.Handlers.HandshakeHandlers.SetCertificatePath(certPath);
        }
        else
        {
            // Try one more: absolute path if we know we are in e:\Cs\Nalix
            if (System.IO.File.Exists(@"e:\Cs\Nalix\shared\certificate.private"))
            {
                Nalix.Runtime.Handlers.HandshakeHandlers.SetCertificatePath(@"e:\Cs\Nalix\shared\certificate.private");
            }
        }
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

        while (current != null)
        {
            string candidate = System.IO.Path.Combine(current, "shared", "certificate.public");
            if (System.IO.File.Exists(candidate))
            {
                pubPath = candidate;
                break;
            }
            current = System.IO.Path.GetDirectoryName(current);
        }

        if (pubPath == null)
        {
            if (System.IO.File.Exists(@"e:\Cs\Nalix\shared\certificate.public"))
            {
                pubPath = @"e:\Cs\Nalix\shared\certificate.public";
            }
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

        throw new System.IO.FileNotFoundException("Public key file not found.");
    }
}

/// <summary>
/// A simple protocol for integration testing that dispatches packets to the Nalix runtime.
/// Copied from NetworkApplicationIntegrationTests to satisfy "real server" requirement in SDK tests.
/// </summary>
public sealed class IntegrationTestProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    public IntegrationTestProtocol(IPacketDispatch dispatch)
    {
        _dispatch = dispatch;
        this.KeepConnectionOpen = true;
        this.SetConnectionAcceptance(true);
    }

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
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

internal sealed class FakeSession(bool isConnected) : TransportSession
{
    private readonly FakePacketRegistry _catalog = new();
    public override TransportOptions Options { get; } = new();
    public override IPacketRegistry Catalog => _catalog;
    public override bool IsConnected { get; } = isConnected;
    public int SendPacketCallCount { get; private set; }

    public override event EventHandler? OnConnected;
    public override event EventHandler<Exception>? OnDisconnected;
    public override event EventHandler<IBufferLease>? OnMessageReceived;
    public override event EventHandler<Exception>? OnError;

    public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default) => Task.CompletedTask;
    public override Task DisconnectAsync() => Task.CompletedTask;

    public override Task SendAsync(IPacket packet, CancellationToken ct = default)
    {
        SendPacketCallCount++;
        if (packet is Control ping && _catalog.TryDequeue(out IPacket? response) && response is Control pong)
        {
            pong.SequenceId = ping.SequenceId;

            byte[] data = new byte[PacketConstants.HeaderSize];
            uint magic = PacketRegistryFactory.Compute(response.GetType());
            BinaryPrimitives.WriteUInt32LittleEndian(data, magic);

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
