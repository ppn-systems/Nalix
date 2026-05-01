// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.DataFrames;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Pooling;
using Nalix.Codec.DataFrames.SignalFrames;
using Xunit;

namespace Nalix.Codec.Tests.DataFrames;

public sealed class PacketPoolLeaseTests
{
    [Fact]
    public void RentReturnsLeaseAndDisposeIsIdempotent()
    {
        using PacketScope<Control> lease = PacketFactory<Control>.Acquire();

        Assert.NotNull(lease.Value);

        // explicit second dispose should be ignored
        lease.Dispose();
        lease.Dispose();
    }

    [Fact]
    public void PreallocAndClearDoNotThrowAndReturnNonNegativeCounts()
    {
        ObjectPoolManager manager = new();
        PacketRegistry.Configure(manager);

        int preallocated = manager.Prealloc<Control>(3);
        int cleared = manager.ClearPool<Control>();

        Assert.True(preallocated >= 0);
        Assert.True(cleared >= 0);

        PacketRegistry.Configure(null!);
    }
}
















