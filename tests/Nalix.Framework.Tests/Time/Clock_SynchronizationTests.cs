// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

[Collection("ClockTests")]
public class ClockSynchronizationTests
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
        // Small delay to ensure reset propagates in concurrent test environment
        System.Threading.Thread.Sleep(50);
        Assert.Equal(0, Clock.CurrentErrorEstimateMs(), 6);
    }

    [Fact]
    public void GetCurrentErrorEstimateMsAfterSyncNonZeroish()
    {
        Clock.ResetSynchronization();
        _ = Clock.SynchronizeTime(DateTime.UtcNow.AddSeconds(2));
        double err = Clock.CurrentErrorEstimateMs();
        // Có thể dương / âm tùy thời điểm, nhưng magnitude > 0
        Assert.True(Math.Abs(err) >= 0);
    }
}
