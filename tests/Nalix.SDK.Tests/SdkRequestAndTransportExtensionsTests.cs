using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Nalix.Framework.DataFrames;
using System.Buffers.Binary;
using Xunit;

namespace Nalix.SDK.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class SdkRequestAndTransportExtensionsTests
{
    [Fact]
    public async Task RequestAsync_WhenClientIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await RequestExtensions.RequestAsync<Control>(
                client: null!,
                request: new Control(),
                options: RequestOptions.Default,
                predicate: _ => true,
                ct: CancellationToken.None));
    }

    [Fact]
    public async Task RequestAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        FakeSession session = new(isConnected: true);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session.RequestAsync<Control>(
                request: null!,
                options: RequestOptions.Default,
                predicate: _ => true,
                ct: CancellationToken.None));
    }

    [Fact]
    public async Task RequestAsync_WhenClientDisconnected_ThrowsNetworkException()
    {
        FakeSession session = new(isConnected: false);

        await Assert.ThrowsAsync<NetworkException>(async () =>
            await session.RequestAsync<Control>(
                request: new Control(),
                options: RequestOptions.Default,
                predicate: _ => true,
                ct: CancellationToken.None));
    }

    [Fact]
    public async Task RequestAsync_WhenEncryptRequestedOnNonTcpSession_ThrowsArgumentException()
    {
        FakeSession session = new(isConnected: true);
        RequestOptions options = RequestOptions.Default.WithEncrypt();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await session.RequestAsync<Control>(
                request: new Control(),
                options: options,
                predicate: _ => true,
                ct: CancellationToken.None));
    }

    [Fact]
    public async Task RequestAsync_WhenTimeoutOccurs_RetriesExactlyRetryCountPlusOne()
    {
        FakeSession session = new(isConnected: true);
        RequestOptions options = RequestOptions.Default.WithTimeout(20).WithRetry(2);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await session.RequestAsync<Control>(
                request: new Control(),
                options: options,
                predicate: _ => true,
                ct: CancellationToken.None));

        Assert.Equal(3, session.SendPacketCallCount);
    }

    [Fact]
    public async Task RequestAsync_WhenSendFailsWithNonTimeout_DoesNotRetryAndPropagates()
    {
        FakeSession session = new(isConnected: true)
        {
            SendPacketException = new FormatException("send failed")
        };
        RequestOptions options = RequestOptions.Default.WithTimeout(1000).WithRetry(5);

        FormatException ex = await Assert.ThrowsAsync<FormatException>(async () =>
            await session.RequestAsync<Control>(
                request: new Control(),
                options: options,
                predicate: _ => true,
                ct: CancellationToken.None));

        Assert.Equal("send failed", ex.Message);
        Assert.Equal(1, session.SendPacketCallCount);
    }

    [Fact]
    public async Task RequestAsync_WhenPredicateIsNull_UsesDefaultAndReturnsFirstMatchingResponse()
    {
        FakeSession session = new(isConnected: true);
        Control response = new() { Type = ControlType.PONG };
        session.EnqueueNextPacket(response);

        Control result = await session.RequestAsync<Control>(
            request: new Control(),
            options: RequestOptions.Default.WithTimeout(1000),
            predicate: null,
            ct: CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(1, session.SendPacketCallCount);
    }

    [Fact]
    public async Task PingAsync_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        TcpSession? session = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session!.PingAsync(timeoutMs: 1000, ct: CancellationToken.None));
    }

    [Fact]
    public async Task SyncTimeAsync_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        TcpSession? session = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session!.SyncTimeAsync(timeoutMs: 1000, ct: CancellationToken.None));
    }

    private sealed class FakeSession(bool isConnected) : TransportSession
    {
        private readonly FakePacketRegistry _catalog = new();

        public override TransportOptions Options { get; } = new();
        public override IPacketRegistry Catalog => _catalog;
        public override bool IsConnected { get; } = isConnected;

        public int SendPacketCallCount { get; private set; }
        public Exception? SendPacketException { get; set; }

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
            OnDisconnected?.Invoke(this, new InvalidOperationException("closed"));
            return Task.CompletedTask;
        }

        public override Task SendAsync(IPacket packet, CancellationToken ct = default)
        {
            SendPacketCallCount++;
            if (SendPacketException is not null)
            {
                throw SendPacketException;
            }

            if (_catalog.TryDequeue(out IPacket? response) && response is not null)
            {
                byte[] data = new byte[PacketConstants.HeaderSize];
                uint magic = PacketRegistryFactory.Compute(response.GetType());
                BinaryPrimitives.WriteUInt32LittleEndian(data, magic);

                using BufferLease lease = BufferLease.CopyFrom(data);
                OnMessageReceived?.Invoke(this, lease);
            }


            return Task.CompletedTask;
        }

        public override Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
        {
            SendPacketCallCount++;
            if (SendPacketException is not null)
            {
                throw SendPacketException;
            }

            if (_catalog.TryDequeue(out IPacket? response) && response is not null)
            {
                byte[] data = new byte[PacketConstants.HeaderSize];
                uint magic = PacketRegistryFactory.Compute(response.GetType());
                BinaryPrimitives.WriteUInt32LittleEndian(data, magic);

                using BufferLease lease = BufferLease.CopyFrom(data);
                OnMessageReceived?.Invoke(this, lease);
            }


            return Task.CompletedTask;
        }

        public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public void EnqueueNextPacket(IPacket packet) => _catalog.Enqueue(packet);

        protected override void Dispose(bool disposing)
        {
        }
    }

    private sealed class FakePacketRegistry : IPacketRegistry
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
            if (ok)
            {
                _lastDequeued = packet;
            }
            return ok;
        }

        public IPacket Deserialize(ReadOnlySpan<byte> raw)
            => _lastDequeued ?? (_queue.TryPeek(out IPacket? packet) ? packet : new Control());

        public bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
        {
            packet = _lastDequeued ?? (_queue.TryPeek(out IPacket? p) ? p : new Control());
            return true;
        }
    }
}
