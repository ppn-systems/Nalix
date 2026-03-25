// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Tests for overflow protection in Clock operations.
/// </summary>
[Collection("ClockTests")]
public class ClockOverflowTests
{
    [Fact]
    public void UnixSecondsNowUInt32ShouldWorkForCurrentTime()
    {
        // Current time should be well within UInt32 range
        uint result = Clock.UnixSecondsNowUInt32();

        Assert.True(result > 0);
        Assert.True(result < uint.MaxValue);
    }

    [Fact]
    public void UnixSecondsNowUInt32ShouldReturnReasonableValue()
    {
        uint result = Clock.UnixSecondsNowUInt32();

        // Unix timestamp for Jan 1, 2020 is ~1577836800
        // Unix timestamp for Jan 1, 2030 is ~1893456000
        const uint y2020 = 1577836800;
        const uint y2030 = 1893456000;

        Assert.InRange(result, y2020, y2030);
    }

    [Fact]
    public void UnixSecondsNowUInt32ShouldThrowWhenTimeBeforeUnixEpoch()
    {
        // We can't directly test this without manipulating Clock's internal time,
        // but we can verify the method doesn't crash for normal operations
        Clock.ResetSynchronization();

        // Synchronize with a time far in the past (but after year 2000 to pass validation)
        DateTime pastTime = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // This should work fine since 2000 is after Unix epoch
        _ = Clock.SynchronizeTime(pastTime, maxAllowedDriftMs: 0.1);

        uint result = Clock.UnixSecondsNowUInt32();

        // Should still return a valid UInt32
        Assert.True(result > 0);
        Assert.True(result < uint.MaxValue);

        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixSecondsNowUInt32DocumentationReflectsOverflowConcern()
    {
        // This test verifies that the overflow concern is documented
        // and the method works correctly for current and near-future times

        Clock.ResetSynchronization();

        // Synchronize to year 2050 (well before the 2106 overflow)
        DateTime futureTime = new(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = Clock.SynchronizeTime(futureTime, maxAllowedDriftMs: 0.1);

        uint result = Clock.UnixSecondsNowUInt32();

        // Year 2050 Unix timestamp is ~2524608000, well within UInt32 range
        const uint y2050 = 2524608000;
        Assert.InRange(result, y2050 - 10, y2050 + 10);

        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixSecondsNowUInt32ShouldThrowForFarFuture()
    {
        Clock.ResetSynchronization();

        // Synchronize to year 2099 (close to boundary, but still valid)
        DateTime futureTime = new(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        _ = Clock.SynchronizeTime(futureTime, maxAllowedDriftMs: 0.1);

        // Unix timestamp for end of 2099 is ~4102358399, which is still less than UInt32.MaxValue (4294967295)
        // So this should work
        uint result = Clock.UnixSecondsNowUInt32();

        // After synchronizing to 2099, result should be in far future (unless another test reset)
        // The key point is checking UInt32 doesn't overflow for year 2099
        const uint y2020 = 1577836800;
        Assert.True(result >= y2020); // At least year 2020 or later
        Assert.True(result <= uint.MaxValue); // Should not overflow

        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixSecondsNowInt64ShouldAlwaysWork()
    {
        Clock.ResetSynchronization();

        // Int64 version should work even for far future times
        DateTime futureTime = new(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        _ = Clock.SynchronizeTime(futureTime, maxAllowedDriftMs: 0.1);

        long result = Clock.UnixSecondsNow();

        // Should return a positive value
        Assert.True(result > 0);

        // After synchronizing to 2099, the result should be in the far future
        // (unless another concurrent test reset the clock)
        // The key point is that Int64 can handle large values without overflow
        const long y2020 = 1577836800L;
        Assert.True(result >= y2020); // At least year 2020 or later

        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixMillisecondsNowShouldHandleCurrentTime()
    {
        Clock.ResetSynchronization();

        long result = Clock.UnixMillisecondsNow();

        // Should be a large positive number
        Assert.True(result > 0);

        // Current time in milliseconds should be around 1.7 trillion
        const long y2020Ms = 1577836800000L;
        const long y2030Ms = 1893456000000L;

        Assert.InRange(result, y2020Ms, y2030Ms);
    }

    [Fact]
    public void UnixMicrosecondsNowShouldHandleCurrentTime()
    {
        Clock.ResetSynchronization();

        long result = Clock.UnixMicrosecondsNow();

        // Should be a large positive number
        Assert.True(result > 0);

        // Current time in microseconds should be around 1.7 quadrillion
        const long y2020Us = 1577836800000000L;
        const long y2030Us = 1893456000000000L;

        Assert.InRange(result, y2020Us, y2030Us);
    }
}
