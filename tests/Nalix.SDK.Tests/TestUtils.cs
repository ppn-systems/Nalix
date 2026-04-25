using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Tests;

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
            // Align sequence ID to match predicate
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
