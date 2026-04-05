// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Threading;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

[Collection("ClockTests")]
public sealed class ClockSynchronizationTests
{
    [Fact]
    public void SynchronizeTimeRequiresUtc()
    {
        DateTime local = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);
        _ = Assert.Throws<ArgumentException>(() => Clock.SynchronizeTime(local));
    }

    [Fact]
    public void GetCurrentErrorEstimateMsNoSyncZero()
    {
        Clock.ResetSynchronization();
        Thread.Sleep(50);
        Assert.Equal(0, Clock.CurrentErrorEstimateMs(), 6);
    }

    [Fact]
    public void GetCurrentErrorEstimateMsAfterSyncReflectsOffset()
    {
        Clock.ResetSynchronization();
        _ = Clock.SynchronizeTime(DateTime.UtcNow.AddSeconds(2));
        double err = Clock.CurrentErrorEstimateMs();
        Assert.True(Math.Abs(err) > 100);
    }

    [Fact]
    public void SynchronizeTimeWhenDriftIsWithinThresholdReturnsZero()
    {
        Clock.ResetSynchronization();

        double drift = Clock.SynchronizeTime(DateTime.UtcNow.AddMilliseconds(10), maxAllowedDriftMs: 100);

        Assert.Equal(0, drift, precision: 6);
        Assert.False(Clock.IsSynchronized);
    }

    [Fact]
    public void ResetSynchronizationClearsCurrentErrorEstimate()
    {
        Clock.ResetSynchronization();
        _ = Clock.SynchronizeTime(DateTime.UtcNow.AddSeconds(1), maxAllowedDriftMs: 0.1);
        Assert.True(Math.Abs(Clock.CurrentErrorEstimateMs()) > 100);

        Clock.ResetSynchronization();
        Thread.Sleep(20);

        Assert.Equal(0, Clock.CurrentErrorEstimateMs(), 6);
    }
}
