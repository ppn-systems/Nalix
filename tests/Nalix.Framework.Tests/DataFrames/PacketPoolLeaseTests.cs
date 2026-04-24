// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    public void PreallocAndClearDoNotThrowAndReturnNonNegativeCounts()
    {
        int preallocated = PacketPool<Control>.Prealloc(3);
        int cleared = PacketPool<Control>.Clear();

        Assert.True(preallocated >= 0);
        Assert.True(cleared >= 0);
    }
}
