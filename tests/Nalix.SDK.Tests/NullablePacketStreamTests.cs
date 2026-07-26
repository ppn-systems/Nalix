// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Memory;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed partial class NullablePacketStreamTests
{
    public NullablePacketStreamTests()
    {
        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }
    }

    [Fact]
    public async Task StreamAsyncWhenTerminalNullableFieldIsNullYieldsItemAndCompletes()
    {
        NullableStreamRequest request = new();
        request.Header = request.Header with { SequenceId = 1001 };

        NullableStreamItem response = new()
        {
            IsEndOfStream = true,
            Capacity = null,
            Duration = TimeSpan.FromSeconds(1)
        };
        response.Header = response.Header with { SequenceId = request.Header.SequenceId };

        NullableStreamItem result = await ReadSingleStreamItemAsync(request, response);

        Assert.True(result.IsEndOfStream);
        Assert.Null(result.Capacity);
        Assert.Equal(TimeSpan.FromSeconds(1), result.Duration);
    }

    [Fact]
    public async Task StreamAsyncWhenTerminalNullableFieldHasValueYieldsItemAndCompletes()
    {
        NullableStreamRequest request = new();
        request.Header = request.Header with { SequenceId = 1002 };

        NullableStreamItem response = new()
        {
            IsEndOfStream = true,
            Capacity = 128,
            Duration = null
        };
        response.Header = response.Header with { SequenceId = request.Header.SequenceId };

        NullableStreamItem result = await ReadSingleStreamItemAsync(request, response);

        Assert.True(result.IsEndOfStream);
        Assert.Equal(128, result.Capacity);
        Assert.Null(result.Duration);
    }

    [Fact]
    public async Task StreamAsyncWhenSequenceDoesNotMatchDisposesIgnoredPacket()
    {
        NullableStreamItem.DisposeCount = 0;

        NullableStreamRequest request = new();
        request.Header = request.Header with { SequenceId = 1003 };

        NullableStreamItem ignored = new()
        {
            IsEndOfStream = false,
            Capacity = 64,
            Duration = null
        };
        ignored.Header = ignored.Header with { SequenceId = 9999 };

        NullableStreamItem response = new()
        {
            IsEndOfStream = true,
            Capacity = 128,
            Duration = null
        };
        response.Header = response.Header with { SequenceId = request.Header.SequenceId };

        FakeStreamSession session = new(ignored, response);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));

        List<NullableStreamSnapshot> snapshots = await ReadAllSnapshotsAsync(session, request, cts.Token);

        NullableStreamSnapshot snapshot = Assert.Single(snapshots);
        Assert.Equal(request.Header.SequenceId, snapshot.SequenceId);
        Assert.Equal(1, NullableStreamItem.DisposeCount);
    }

    [Fact]
    public async Task SubscribeChannelWhenBoundedChannelDropsWriteDisposesDroppedPacket()
    {
        NullableStreamItem.DisposeCount = 0;

        FakeStreamSession session = new();
        ChannelReader<NullableStreamItem> reader = session.SubscribeChannel<NullableStreamItem>(
            boundedCapacity: 1,
            fullMode: BoundedChannelFullMode.DropWrite);

        session.Emit(new NullableStreamItem { IsEndOfStream = false, Capacity = 1 });
        session.Emit(new NullableStreamItem { IsEndOfStream = false, Capacity = 2 });

        NullableStreamItem retained = await reader.ReadAsync();

        Assert.Equal(1, retained.Capacity);
        Assert.Equal(1, NullableStreamItem.DisposeCount);
    }

    private static async Task<NullableStreamItem> ReadSingleStreamItemAsync(NullableStreamRequest request, NullableStreamItem response)
    {
        FakeStreamSession session = new(response);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));

        Task<List<NullableStreamSnapshot>> readTask = ReadAllSnapshotsAsync(session, request, cts.Token);
        Task completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(3), cts.Token));

        Assert.Same(readTask, completed);

        List<NullableStreamSnapshot> snapshots = await readTask;
        NullableStreamSnapshot snapshot = Assert.Single(snapshots);

        Assert.Equal(response.Header.SequenceId, snapshot.SequenceId);

        return new NullableStreamItem
        {
            Header = new PacketHeader { SequenceId = snapshot.SequenceId },
            IsEndOfStream = snapshot.IsEndOfStream,
            Capacity = snapshot.Capacity,
            Duration = snapshot.Duration
        };
    }

    private static async Task<List<NullableStreamSnapshot>> ReadAllSnapshotsAsync(
        FakeStreamSession session,
        NullableStreamRequest request,
        CancellationToken cancellationToken)
    {
        List<NullableStreamSnapshot> snapshots = [];

        await foreach (NullableStreamItem item in session.StreamAsync<NullableStreamItem>(request, ct: cancellationToken))
        {
            snapshots.Add(new NullableStreamSnapshot(
                item.Header.SequenceId,
                item.IsEndOfStream,
                item.Capacity,
                item.Duration));
        }

        return snapshots;
    }

    private sealed class FakeStreamSession(params NullableStreamItem[] responses) : TransportSession
    {
        public override TransportOptions Options { get; } = new();
        public override bool IsConnected => true;

        public override event EventHandler? OnConnected
        {
            add { }
            remove { }
        }

        public override event EventHandler<Exception>? OnDisconnected
        {
            add { }
            remove { }
        }

        public override event EventHandler<IBufferLease>? OnMessageReceived;

        public override event EventHandler<Exception>? OnError
        {
            add { }
            remove { }
        }

        public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task DisconnectAsync() => Task.CompletedTask;

        public override Task SendAsync(IPacket packet, CancellationToken ct = default)
            => this.SendAsync(packet, encrypt: null, ct);

        public override Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
        {
            foreach (NullableStreamItem response in responses)
            {
                Emit(response);
            }

            return Task.CompletedTask;
        }

        public void Emit(NullableStreamItem response)
        {
            byte[] data = response.Serialize();
            using BufferLease lease = BufferLease.CopyFrom(data);
            this.OnMessageReceived?.Invoke(this, lease);
        }

        public override Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override void ResetSequenceCounters() { }

        protected override void Dispose(bool disposing) { }
    }

    private readonly record struct NullableStreamSnapshot(
        ushort SequenceId,
        bool IsEndOfStream,
        int? Capacity,
        TimeSpan? Duration);

    [Packet]
    [GenerateFormatter]
    [SerializePackable(SerializeLayout.Explicit)]
    public sealed partial class NullableStreamRequest : PacketBase<NullableStreamRequest>, IPacketStaticOpcode
    {
        public static ushort StaticOpCode => 0x7A89;
    }

    [Packet]
    [GenerateFormatter]
    [SerializePackable(SerializeLayout.Explicit)]
    public sealed partial class NullableStreamItem : PacketBase<NullableStreamItem>, IPacketStaticOpcode, IPacketStreamable
    {
        public static ushort StaticOpCode => 0x7A8A;
        public static int DisposeCount { get; set; }

        [SerializeOrder(0)]
        public bool IsEndOfStream { get; set; }

        [SerializeOrder(1)]
        public int? Capacity { get; set; }

        [SerializeOrder(2)]
        public TimeSpan? Duration { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }
}
