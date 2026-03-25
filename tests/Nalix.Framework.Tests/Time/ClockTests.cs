// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

[Collection("ClockTests")]
public class ClockTests
{
    [Fact]
    public void UnixSecondsNowShouldMatch()
    {
        DateTime now = DateTime.UtcNow;
        long unix = Clock.UnixSecondsNow();
        long expected = (long)(now - DateTime.UnixEpoch).TotalSeconds;
        Assert.InRange(unix, expected - 1, expected + 1);
    }

    [Fact]
    public void UnixMillisecondsNowShouldMatchRawUtcWindow()
    {
        long unix = Clock.UnixMillisecondsNow();
        DateTime now = DateTime.UtcNow;
        long expected = (long)(now - DateTime.UnixEpoch).TotalMilliseconds;

        // 2.5s đủ an toàn cho các môi trường có NTP/VM jitter
        Assert.InRange(unix, expected - 2500, expected + 2500);
    }

    [Fact]
    public void UnixMicrosecondsNowShouldMatch()
    {
        DateTime now = DateTime.UtcNow;
        long unix = Clock.UnixMicrosecondsNow();
        long expected = (now - DateTime.UnixEpoch).Ticks / 10;
        Assert.InRange(unix, expected - 500, expected + 2500); // Sai số microsecond
    }

    [Fact]
    public void UnixTicksNowShouldMatch()
    {
        DateTime now = DateTime.UtcNow;
        long unix = Clock.UnixTicksNow();
        long expected = (now - DateTime.UnixEpoch).Ticks;
        Assert.InRange(unix, expected - 10000, expected + 10000);
    }

    [Fact]
    public void SynchronizeTimeAndResetSynchronization()
    {
        DateTime now = DateTime.UtcNow.AddSeconds(1);
        double drift = Clock.SynchronizeTime(now, 0.1);
        Assert.True(Math.Abs(drift) > 0);
        Assert.True(Clock.IsSynchronized);
        Assert.True(Clock.LastSyncTime == now);

        Clock.ResetSynchronization();
        Assert.False(Clock.IsSynchronized);
        Assert.Equal(DateTime.MinValue, Clock.LastSyncTime);
    }
}