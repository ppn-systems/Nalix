using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class PacketAwaiterTests
{
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

    private sealed class FakeSession : TransportSession
    {
        private readonly IPacketRegistry _catalog = new PacketRegistryFactory().CreateCatalog();

        public override TransportOptions Options { get; } = new();

        public override IPacketRegistry Catalog => _catalog;

        public override bool IsConnected => true;

        public override event EventHandler? OnConnected;

        public override event EventHandler<Exception>? OnDisconnected;

        public override event EventHandler<IBufferLease>? OnMessageReceived
        {
            add { }
            remove { }
        }

        public override event EventHandler<Exception>? OnError;

        public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task DisconnectAsync()
        {
            this.OnDisconnected?.Invoke(this, new InvalidOperationException("session closed"));
            return Task.CompletedTask;
        }

        public override Task SendAsync(IPacket packet, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public override void Dispose()
        {
        }

        public void TriggerDisconnect(Exception ex) => this.OnDisconnected?.Invoke(this, ex);
    }
}
