using Nalix.Codec.Memory;
#if DEBUG
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Internal;
using Nalix.SDK.Transport.Extensions;
using Nalix.Abstractions;
using Xunit;
using NSubstitute;

namespace Nalix.SDK.Tests;

public sealed class PacketAwaiterTests
{
    [Fact]
    public async Task AwaitAsync_WhenPredicateMatches_ReturnsPacket()
    {
        TransportSession session = Substitute.For<TransportSession>();
        TestPacket packet = new() { Header = new PacketHeader { OpCode = 0x100 } };
        
        session.Catalog.Returns(new ManualCatalog(packet));

        ManualLease lease = new();
        uint magic = Nalix.Codec.DataFrames.PacketRegistryFactory.Compute(typeof(TestPacket));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(lease.Span, magic);

        Task<TestPacket> awaitTask = PacketAwaiter.AwaitAsync<TestPacket>(
            session,
            p => p.Header.OpCode == 0x100,
            1000,
            ct => Task.CompletedTask,
            CancellationToken.None);

        // Trigger the message received event
        session.OnMessageReceived += Raise.Event<EventHandler<IBufferLease>>(session, lease);

        TestPacket result = await awaitTask;
        Assert.Same(packet, result);
    }

    private sealed class ManualLease : IBufferLease
    {
        private byte[] _data = new byte[16];
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
















