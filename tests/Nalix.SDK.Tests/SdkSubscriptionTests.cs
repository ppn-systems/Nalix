using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Codec.ProtocolFrames;
using Nalix.Environment.Memory;
using Nalix.SDK.Extensions;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class SdkSubscriptionTests
{
    [Fact]
    public void SubscriptionExtensionsSubscribeTemp_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        TransportSession? session = null;

        _ = Assert.Throws<ArgumentNullException>(() =>
            session!.SubscribeTemp(
                onMessageReceived: (_, _) => { },
                onDisconnected: (_, _) => { }));
    }

    [Fact]
    public void SubscriptionExtensionsSubscribeTemp_DisposeUnsubscribesHandlers()
    {
        FakeSession session = new();
        int messageCount = 0;
        int disconnectCount = 0;

        IDisposable sub = session.SubscribeTemp(
            onMessageReceived: (_, _) => messageCount++,
            onDisconnected: (_, _) => disconnectCount++);

        session.RaiseMessage();
        session.RaiseDisconnect(new InvalidOperationException("d1"));
        sub.Dispose();
        session.RaiseMessage();
        session.RaiseDisconnect(new InvalidOperationException("d2"));

        Assert.Equal(1, messageCount);
        Assert.Equal(1, disconnectCount);
    }

    [Fact]
    public void TcpSessionSubscriptionsOn_WhenNullArguments_ThrowArgumentNullException()
    {
        FakeSession session = new();

        _ = Assert.Throws<ArgumentNullException>(() => TransportSessionSubscriptions.On<Control>(null!, _ => { }));
        _ = Assert.Throws<ArgumentNullException>(() => session.On<Control>(null!));
    }

    [Fact]
#pragma warning disable CS0618
    public void TcpSessionSubscriptionsOnOnce_FiresOnlyOnceEvenWhenMultipleMessagesArrive()
    {
        FakeSession session = new();

        if (!PacketRegistry.IsBuilt)
            session.EnsureRegistry();
        int count = 0;
        using IDisposable sub = session.OnOnce<Control>(_ => true, _ => count++);

        session.SetNextPacket(new Control());
        session.RaiseMessage();
        session.SetNextPacket(new Control());
        session.RaiseMessage();

        Assert.Equal(1, count);
    }
#pragma warning restore CS0618

    [Fact]
    public void TcpSessionSubscriptionsOn_WhenPredicateThrows_DoesNotPropagateToCaller()
    {
        FakeSession session = new();
        using IDisposable sub = session.On(_ => throw new InvalidOperationException("predicate failed"), _ => { });

        Exception? ex = Record.Exception(session.RaiseMessage);

        Assert.Null(ex);
    }

    [Fact]
    public void CompositeSubscriptionDispose_WhenOneDisposableThrows_DisposesRemainingWithoutThrowing()
    {
        TrackingDisposable ok1 = new();
        ThrowingDisposable bad = new();
        TrackingDisposable ok2 = new();
        CompositeSubscription composite = new(ok1, bad, ok2);

        Exception? ex = Record.Exception(composite.Dispose);

        Assert.Null(ex);
        Assert.True(ok1.Disposed);
        Assert.True(ok2.Disposed);
    }

    private sealed class FakeSession : TransportSession
    {
        private readonly FakePacketRegistry _catalog = new();

        public override TransportOptions Options { get; } = new();
        public override bool IsConnected => true;

        public override event EventHandler? OnConnected
        {
            add { }
            remove { }
        }
        public override event EventHandler<Exception>? OnDisconnected;
        public override event EventHandler<IBufferLease>? OnMessageReceived;
        public override event EventHandler<Exception>? OnError
        {
            add { }
            remove { }
        }

        public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task DisconnectAsync()
        {
            OnDisconnected?.Invoke(this, new InvalidOperationException("disconnect"));
            return Task.CompletedTask;
        }

        public override Task SendAsync(IPacket packet, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override void ResetSequenceCounters() { }

        protected override void Dispose(bool disposing)
        {
        }

        public void SetNextPacket(IPacket packet) => _catalog.Next = packet;

        public void EnsureRegistry() => PacketRegistry.Build();

        public void RaiseMessage()
        {
            byte[] data = _catalog.Next.Serialize();

            using BufferLease lease = BufferLease.CopyFrom(data);
            OnMessageReceived?.Invoke(this, lease);
        }

        public void RaiseDisconnect(Exception ex) => OnDisconnected?.Invoke(this, ex);
    }

    private sealed class FakePacketRegistry
    {
        public IPacket Next { get; set; } = new Control();
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("dispose failed");
    }
}


















