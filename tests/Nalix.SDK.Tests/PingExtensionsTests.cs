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
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Xunit;

namespace Nalix.SDK.Tests;

public sealed class PingExtensionsTests
{
    [Fact]
    public async Task PingAsync_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        TransportSession? session = null;
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await session!.PingAsync());
    }

    [Fact]
    public async Task PingAsync_WhenSuccessful_ReturnsPositiveRtt()
    {
        FakeSession session = new(isConnected: true);
        
        // Prepare PONG response
        Control pong = Control.Create();
        pong.Initialize(0, ControlType.PONG, sequenceId: 1, flags: PacketFlags.RELIABLE, reasonCode: ProtocolReason.NONE);
        session.EnqueueNextPacket(pong);

        double rtt = await session.PingAsync(timeoutMs: 1000);
        
        Assert.True(rtt >= 0);
        Assert.Equal(1, session.SendPacketCallCount);
    }

    [Fact]
    public async Task PingAsync_WhenTimeout_ThrowsTimeoutException()
    {
        FakeSession session = new(isConnected: true);
        // No response enqueued
        
        await Assert.ThrowsAsync<TimeoutException>(async () => await session.PingAsync(timeoutMs: 10));
    }

}
#if DEBUG
#endif
#if DEBUG
#endif
