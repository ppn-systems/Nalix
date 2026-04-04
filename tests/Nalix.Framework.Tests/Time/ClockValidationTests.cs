// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Tests for input validation in Clock operations.
/// </summary>
[Collection("ClockTests")]
public sealed class ClockValidationTests
{
    [Fact]
    public void SynchronizeUnixMillisecondsRejectsNegativeServerTime()
    {
        Clock.ResetSynchronization();

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(-1000, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp cannot be negative", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsRejectsNegativeRtt()
    {
        Clock.ResetSynchronization();

        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, -10, 1000, 10000));

        Assert.Contains("RTT cannot be negative", ex.Message);
        Assert.Equal("rttMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsRejectsZeroMaxAllowedDrift()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 0, 10000));

        Assert.Contains("Max allowed drift must be positive", ex.Message);
        Assert.Equal("maxAllowedDriftMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsRejectsNegativeMaxAllowedDrift()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, -1000, 10000));

        Assert.Contains("Max allowed drift must be positive", ex.Message);
        Assert.Equal("maxAllowedDriftMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsRejectsZeroMaxHardAdjust()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 1000, 0));

        Assert.Contains("Max hard adjust must be positive", ex.Message);
        Assert.Equal("maxHardAdjustMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsRejectsNegativeMaxHardAdjust()
    {
        Clock.ResetSynchronization();
        long currentUnixMs = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(currentUnixMs, 0, 1000, -10000));

        Assert.Contains("Max hard adjust must be positive", ex.Message);
        Assert.Equal("maxHardAdjustMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsRejectsTimestampBeforeYear2000()
    {
        Clock.ResetSynchronization();

        const long oldTimestamp = 915148800000L;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(oldTimestamp, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp is outside reasonable range", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsRejectsTimestampAfterYear2100()
    {
        Clock.ResetSynchronization();

        const long futureTimestamp = 4133980800000L;

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Clock.SynchronizeUnixMilliseconds(futureTimestamp, 0, 1000, 10000));

        Assert.Contains("Server Unix timestamp is outside reasonable range", ex.Message);
        Assert.Equal("serverUnixMs", ex.ParamName);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsAcceptsReasonableCurrentTimestamp()
    {
        Clock.ResetSynchronization();
        Thread.Sleep(10);
        long futureUnixMs = (long)(DateTime.UtcNow.AddSeconds(10) - DateTime.UnixEpoch).TotalMilliseconds;
        double adjustment = Clock.SynchronizeUnixMilliseconds(futureUnixMs, 10, 1000, 50000);

        Assert.True(Clock.IsSynchronized);
        Assert.InRange(Math.Abs(adjustment), 9000, 11000);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsAcceptsYear2000Boundary()
    {
        Clock.ResetSynchronization();

        const long y2kTimestamp = 946684800000L;
        _ = Clock.SynchronizeUnixMilliseconds(y2kTimestamp, 0, 100000, 100000);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsAcceptsYear2100Boundary()
    {
        Clock.ResetSynchronization();

        const long y2100Timestamp = 4102444800000L;
        _ = Clock.SynchronizeUnixMilliseconds(y2100Timestamp, 0, 100000, 100000);
    }

    [Fact]
    public void SynchronizeUnixMillisecondsAccountsForRoundTripTime()
    {
        Clock.ResetSynchronization();
        Thread.Sleep(10);
        long futureUnixMs = (long)(DateTime.UtcNow.AddSeconds(10) - DateTime.UnixEpoch).TotalMilliseconds;
        const double rttMs = 100;

        double adjustment = Clock.SynchronizeUnixMilliseconds(futureUnixMs, rttMs, 1000, 50000);

        Assert.True(Clock.IsSynchronized);
        Assert.InRange(Math.Abs(adjustment), 9000, 11000);
    }
}
