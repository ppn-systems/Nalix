using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Extensions;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

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

        _ = Assert.Throws<ArgumentNullException>(() => TcpSessionSubscriptions.On<Control>(null!, _ => { }));
        _ = Assert.Throws<ArgumentNullException>(() => session.On<Control>(null!));
    }

    [Fact]
    public void TcpSessionSubscriptionsOnOnce_FiresOnlyOnceEvenWhenMultipleMessagesArrive()
    {
        FakeSession session = new();
        int count = 0;
        using IDisposable sub = session.OnOnce<Control>(_ => true, _ => count++);

        session.SetNextPacket(new Control());
        session.RaiseMessage();
        session.SetNextPacket(new Control());
        session.RaiseMessage();

        Assert.Equal(1, count);
    }

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
        public override IPacketRegistry Catalog => _catalog;
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

        public override void Dispose()
        {
        }

        public void SetNextPacket(IPacket packet) => _catalog.Next = packet;

        public void RaiseMessage()
        {
            using BufferLease lease = BufferLease.CopyFrom([1, 2, 3]);
            OnMessageReceived?.Invoke(this, lease);
        }

        public void RaiseDisconnect(Exception ex) => OnDisconnected?.Invoke(this, ex);
    }

    private sealed class FakePacketRegistry : IPacketRegistry
    {
        public IPacket Next { get; set; } = new Control();

        public int DeserializerCount => 1;
        public bool IsKnownMagic(uint magic) => true;
        public bool IsRegistered<TPacket>() where TPacket : IPacket => true;
        public IPacket Deserialize(ReadOnlySpan<byte> raw) => Next;
        public bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
        {
            packet = Next;
            return true;
        }
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
