#if DEBUG
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Internal;
using Nalix.Framework.DataFrames;
using System.Buffers.Binary;
using Xunit;

namespace Nalix.SDK.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class PacketAwaiterTests
{
    [Fact]
    public async Task AwaitAsync_WhenClientIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await PacketAwaiter.AwaitAsync<Control>(
                client: null!,
                predicate: _ => true,
                timeoutMs: 1000,
                sendAsync: _ => Task.CompletedTask,
                ct: CancellationToken.None));
    }

    [Fact]
    public async Task AwaitAsync_WhenPredicateIsNull_ThrowsArgumentNullException()
    {
        FakeSession session = new();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await PacketAwaiter.AwaitAsync<Control>(
                client: session,
                predicate: null!,
                timeoutMs: 1000,
                sendAsync: _ => Task.CompletedTask,
                ct: CancellationToken.None));
    }

    [Fact]
    public async Task AwaitAsync_WhenSendAsyncIsNull_ThrowsArgumentNullException()
    {
        FakeSession session = new();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await PacketAwaiter.AwaitAsync<Control>(
                client: session,
                predicate: _ => true,
                timeoutMs: 1000,
                sendAsync: null!,
                ct: CancellationToken.None));
    }

    [Fact]
    public async Task AwaitAsync_WhenSessionDisconnects_ThrowsNetworkException()
    {
        FakeSession session = new();

        Func<Task> act = async () =>
        {
            _ = await PacketAwaiter.AwaitAsync<Control>(
                session,
                predicate: _ => true,
                timeoutMs: 5000,
                sendAsync: _ =>
                {
                    session.TriggerDisconnect(new InvalidOperationException("boom"));
                    return Task.CompletedTask;
                },
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<Nalix.Common.Exceptions.NetworkException>()
            .WithMessage("*Disconnected while waiting for Control*");
    }

    [Fact]
    public async Task AwaitAsync_WhenMatchingPacketArrives_ReturnsPacketAndUnsubscribesHandlers()
    {
        FakeSession session = new();
        Control expected = new();

        Control result = await PacketAwaiter.AwaitAsync<Control>(
            session,
            predicate: packet => ReferenceEquals(packet, expected),
            timeoutMs: 5000,
            sendAsync: _ =>
            {
                session.TriggerPacket(new Control());
                session.TriggerPacket(expected);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(0, session.MessageSubscriberCount);
        Assert.Equal(0, session.DisconnectSubscriberCount);
    }

    [Fact]
    public async Task AwaitAsync_WhenTimeoutOccurs_ThrowsTimeoutException()
    {
        FakeSession session = new();

        Func<Task> act = async () =>
        {
            _ = await PacketAwaiter.AwaitAsync<Control>(
                session,
                predicate: _ => true,
                timeoutMs: 50,
                sendAsync: _ => Task.CompletedTask,
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*No Control received within 50 ms*");
    }

    [Fact]
    public async Task AwaitAsync_WhenCallerTokenCancelled_ThrowsOperationCanceledException()
    {
        FakeSession session = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = async () =>
        {
            _ = await PacketAwaiter.AwaitAsync<Control>(
                session,
                predicate: _ => true,
                timeoutMs: 5000,
                sendAsync: _ => Task.CompletedTask,
                cts.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AwaitAsync_WhenPredicateThrows_PropagatesPredicateException()
    {
        FakeSession session = new();

        Func<Task> act = async () =>
        {
            _ = await PacketAwaiter.AwaitAsync<Control>(
                session,
                predicate: _ => throw new InvalidOperationException("predicate boom"),
                timeoutMs: 2000,
                sendAsync: _ =>
                {
                    session.TriggerPacket(new Control());
                    return Task.CompletedTask;
                },
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*predicate boom*");
    }

    [Fact]
    public async Task AwaitAsync_WhenSendThrowsInvalidOperation_WrapsAsNetworkException()
    {
        FakeSession session = new();

        Func<Task> act = async () =>
        {
            _ = await PacketAwaiter.AwaitAsync<Control>(
                session,
                predicate: _ => true,
                timeoutMs: 2000,
                sendAsync: _ => throw new InvalidOperationException("send disconnected"),
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<NetworkException>()
            .WithMessage("*Disconnected while sending Control*");
    }

    [Fact]
    public async Task AwaitAsync_WhenSendThrowsNonConnectionException_PropagatesOriginalException()
    {
        FakeSession session = new();

        Func<Task> act = async () =>
        {
            _ = await PacketAwaiter.AwaitAsync<Control>(
                session,
                predicate: _ => true,
                timeoutMs: 2000,
                sendAsync: _ => throw new FormatException("bad frame"),
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<FormatException>()
            .WithMessage("*bad frame*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task AwaitAsync_WhenTimeoutNegative_ThrowsArgumentOutOfRangeException(int timeoutMs)
    {
        FakeSession session = new();

        Func<Task> act = () => PacketAwaiter.AwaitAsync<Control>(
            session,
            predicate: _ => true,
            timeoutMs: timeoutMs,
            sendAsync: _ => Task.CompletedTask,
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(act);
    }

    private sealed class FakeSession : TransportSession
    {
        private readonly IPacketRegistry _catalog = new FakePacketRegistry();
        private EventHandler<Exception>? _onDisconnected;
        private EventHandler<IBufferLease>? _onMessageReceived;
        private int _messageSubscriberCount;
        private int _disconnectSubscriberCount;

        public override TransportOptions Options { get; } = new();

        public override IPacketRegistry Catalog => _catalog;

        public override bool IsConnected => true;

        public override event EventHandler? OnConnected
        {
            add { }
            remove { }
        }

        public override event EventHandler<Exception>? OnDisconnected
        {
            add
            {
                _onDisconnected += value;
                _disconnectSubscriberCount++;
            }
            remove
            {
                var original = _onDisconnected;
                _onDisconnected -= value;
                if (!ReferenceEquals(original, _onDisconnected))
                {
                    _disconnectSubscriberCount--;
                }
            }
        }

        public override event EventHandler<IBufferLease>? OnMessageReceived
        {
            add
            {
                _onMessageReceived += value;
                _messageSubscriberCount++;
            }
            remove
            {
                var original = _onMessageReceived;
                _onMessageReceived -= value;
                if (!ReferenceEquals(original, _onMessageReceived))
                {
                    _messageSubscriberCount--;
                }
            }
        }

        public override event EventHandler<Exception>? OnError
        {
            add { }
            remove { }
        }

        public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task DisconnectAsync()
        {
            _onDisconnected?.Invoke(this, new InvalidOperationException("session closed"));
            return Task.CompletedTask;
        }

        public override Task SendAsync(IPacket packet, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override void Dispose()
        {
        }

        public int MessageSubscriberCount => _messageSubscriberCount;
        public int DisconnectSubscriberCount => _disconnectSubscriberCount;

        public void TriggerDisconnect(Exception ex) => _onDisconnected?.Invoke(this, ex);

        public void TriggerPacket(IPacket packet)
        {
            FakePacketRegistry.NextPacket = packet;
            
            // Create a buffer that satisfies the dispatcher's magic number check
            byte[] data = new byte[PacketConstants.HeaderSize];
            uint magic = PacketRegistryFactory.Compute(packet.GetType());
            BinaryPrimitives.WriteUInt32LittleEndian(data, magic);
            
            using BufferLease lease = BufferLease.CopyFrom(data);
            _onMessageReceived?.Invoke(this, lease);
        }
    }

    private sealed class FakePacketRegistry : IPacketRegistry
    {
        public static IPacket? NextPacket { get; set; }

        public int DeserializerCount => 1;

        public bool IsKnownMagic(uint magic) => true;

        public bool IsRegistered<TPacket>() where TPacket : IPacket => true;

        public IPacket Deserialize(ReadOnlySpan<byte> raw)
            => NextPacket ?? new Control();

        public bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
        {
            packet = NextPacket ?? new Control();
            return true;
        }
    }
}
#endif
