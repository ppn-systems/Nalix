#if DEBUG
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Internal;
using NSubstitute;

namespace Nalix.SDK.Tests;

public sealed class PacketAwaiterTests
{
    [Fact]
    public async Task AwaitAsync_WhenPredicateMatches_ReturnsPacket()
    {
        TransportSession session = Substitute.For<TransportSession>();

        if (!PacketRegistry.IsBuilt)
            Nalix.Codec.DataFrames.PacketRegistry.Build();
        Nalix.Codec.DataFrames.SignalFrames.Control packet = new();
        var header = packet.Header;
        header.OpCode = 0x100;
        packet.Header = header;
        byte[] data = packet.Serialize();
        ManualLease lease = new(data);

        Task<Nalix.Codec.DataFrames.SignalFrames.Control> awaitTask = PacketAwaiter.AwaitAsync<Nalix.Codec.DataFrames.SignalFrames.Control>(
            session,
            p => p.Header.OpCode == 0x100,
            1000,
            ct => Task.CompletedTask,
            CancellationToken.None);

        // Trigger the message received event
        session.OnMessageReceived += Raise.Event<EventHandler<IBufferLease>>(session, lease);

        Nalix.Codec.DataFrames.SignalFrames.Control result = await awaitTask;
        Assert.Equal(0x100, result.Header.OpCode);
    }

    private sealed class ManualLease : IBufferLease
    {
        private readonly byte[] _data;
        public ManualLease(byte[] data) => _data = data;
        public int Length => _data.Length;
        public bool IsReliable { get; set; }
        public int Capacity => _data.Length;
        public Span<byte> Span => _data;
        public Span<byte> SpanFull => _data;
        public ReadOnlyMemory<byte> Memory => _data;
        public void Dispose() { }
        public void Retain() { }
        public void CommitLength(int length) { }
        public bool ReleaseOwnership(out byte[]? buffer, out int start, out int length)
        {
            buffer = _data;
            start = 0;
            length = _data.Length;
            return true;
        }
    }

    private sealed class ManualCatalog(IPacket result) : IPacketRegistry
    {
        public int DeserializerCount => 1;
        public bool IsKnownMagic(uint magic) => true;
        public bool IsRegistered<TPacket>() where TPacket : IPacket => true;
        public IPacket Deserialize(ReadOnlySpan<byte> buffer) => result;
        public bool TryDeserialize(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out IPacket? packet)
        {
            packet = result;
            return true;
        }
    }

    [Fact]
    public async Task AwaitAsync_WhenTimeoutOccurs_ThrowsTimeoutException()
    {
        TransportSession session = Substitute.For<TransportSession>();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await PacketAwaiter.AwaitAsync<TestPacket>(
                session,
                p => true,
                50,
                ct => Task.Delay(100, ct),
                CancellationToken.None));
    }

    private sealed class TestPacket : IPacket
    {
        public int Length => 0;
        public PacketHeader Header { get; set; }
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
    }
}
#endif

















