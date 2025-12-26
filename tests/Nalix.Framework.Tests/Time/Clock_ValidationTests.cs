// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Tests for input validation in Clock operations.
/// </summary>
[Collection("ClockTests")]
public class ClockValidationTests
{
    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectNegativeServerTime()
    {
        Clock.ResetSynchronization();

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(-1000, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp cannot be negative", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectNegativeRTT()
    {
        Clock.ResetSynchronization();

        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, -10, 1000, 10000));

        Assert.Contains("RTT cannot be negative", ex.Message);
        Assert.Equal("rttMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectZeroMaxAllowedDrift()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 0, 10000));

        Assert.Contains("Max allowed drift must be positive", ex.Message);
        Assert.Equal("maxAllowedDriftMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectNegativeMaxAllowedDrift()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, -1000, 10000));

        Assert.Contains("Max allowed drift must be positive", ex.Message);
        Assert.Equal("maxAllowedDriftMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectZeroMaxHardAdjust()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 1000, 0));

        Assert.Contains("Max hard adjust must be positive", ex.Message);
        Assert.Equal("maxHardAdjustMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectNegativeMaxHardAdjust()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 1000, -10000));

        Assert.Contains("Max hard adjust must be positive", ex.Message);
        Assert.Equal("maxHardAdjustMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectTimestampBeforeYear2000()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 1999 00:00:00
        const long oldTimestamp = 915148800000L;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(oldTimestamp, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp is outside reasonable range", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldRejectTimestampAfterYear2100()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 2101 00:00:00
        const long futureTimestamp = 4133980800000L;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(futureTimestamp, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp is outside reasonable range", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldAcceptValidCurrentTimestamp()
    {
        Clock.ResetSynchronization();

        // Wait a bit to ensure clean state
        System.Threading.Thread.Sleep(10);

        // Use a time slightly in the future to ensure drift threshold is exceeded
        long futureUnixMs = (long)(DateTime.UtcNow.AddSeconds(10) - DateTime.UnixEpoch).TotalMilliseconds;

        // Should not throw - use thresholds that will trigger sync
        double adjustment = Clock.SynchronizeUnixMilliseconds(futureUnixMs, 10, 1000, 50000);

        Assert.True(Clock.IsSynchronized);
        // The adjustment should be around 10 seconds (10000 ms)
        Assert.InRange(Math.Abs(adjustment), 9000, 11000);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldHandleEdgeCaseYear2000()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 2000 00:00:00 (exactly at boundary)
        const long y2kTimestamp = 946684800000L;

        // Should not throw
        _ = Clock.SynchronizeUnixMilliseconds(y2kTimestamp, 0, 100000, 100000);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsShouldHandleEdgeCaseYear2100()
    {
        Clock.ResetSynchronization();

        // Unix timestamp for Jan 1, 2100 00:00:00 (exactly at boundary)
        const long y2100Timestamp = 4102444800000L;

        // Should not throw
        _ = Clock.SynchronizeUnixMilliseconds(y2100Timestamp, 0, 100000, 100000);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsWithRTTShouldAdjustTimeCorrectly()
    {
        Clock.ResetSynchronization();

        // Wait a bit to ensure clean state
        System.Threading.Thread.Sleep(10);

        // Use a time slightly in the future to ensure drift threshold is exceeded
        long futureUnixMs = (long)(DateTime.UtcNow.AddSeconds(10) - DateTime.UnixEpoch).TotalMilliseconds;
        const double rttMs = 100; // 100ms round trip

        // The method should add half of RTT (50ms) to the server time
        // Use thresholds that will trigger sync
        double adjustment = Clock.SynchronizeUnixMilliseconds(futureUnixMs, rttMs, 1000, 50000);

        Assert.True(Clock.IsSynchronized);
        // The adjustment should be around 10 seconds + half RTT (10050 ms)
        Assert.InRange(Math.Abs(adjustment), 9000, 11000);
    }
}
