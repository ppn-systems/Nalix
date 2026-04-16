// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.Pooling;
using Nalix.Framework.DataFrames.SignalFrames;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed class PacketPoolLeaseTests
{
    [Fact]
    public void RentReturnsLeaseAndDisposeIsIdempotent()
    {
        using PacketLease<Control> lease = PacketPool<Control>.Rent();

        Assert.NotNull(lease.Value);

        // explicit second dispose should be ignored
        lease.Dispose();
        lease.Dispose();
    }

    [Fact]
    public void ReturnThenGetResetsControlPacketState()
    {
        _ = PacketPool<Control>.Clear();

        Control packet = PacketPool<Control>.Get();
        packet.Initialize(
            opCode: 1234,
            type: ControlType.DISCONNECT,
            sequenceId: 42,
            reasonCode: ProtocolReason.INTERNAL_ERROR,
            transport: ProtocolType.UDP);
        packet.Priority = PacketPriority.LOW;

        PacketPool<Control>.Return(packet);

        Control reused = PacketPool<Control>.Get();
        try
        {
            Assert.Equal(0u, reused.SequenceId);
            Assert.Equal(ControlType.NONE, reused.Type);
            Assert.Equal(ProtocolReason.NONE, reused.Reason);
            Assert.Equal(PacketPriority.HIGH, reused.Priority);
            Assert.Equal(0L, reused.Timestamp);
            Assert.Equal(0L, reused.MonoTicks);
        }
        finally
        {
            PacketPool<Control>.Return(reused);
        }
    }

    [Fact]
    public void PreallocAndClearDoNotThrowAndReturnNonNegativeCounts()
    {
        int preallocated = PacketPool<Control>.Prealloc(3);
        int cleared = PacketPool<Control>.Clear();

        Assert.True(preallocated >= 0);
        Assert.True(cleared >= 0);
    }
}
