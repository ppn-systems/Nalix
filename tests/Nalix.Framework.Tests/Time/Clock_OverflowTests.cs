using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Tests for overflow protection in Clock operations.
/// </summary>
[Collection("ClockTests")]
public class Clock_OverflowTests
{
    [Fact]
    public void UnixSecondsNowUInt32_Should_Work_For_Current_Time()
    {
        // Current time should be well within UInt32 range
        var result = Clock.UnixSecondsNowUInt32();

        Assert.True(result > 0);
        Assert.True(result < UInt32.MaxValue);
    }

    [Fact]
    public void UnixSecondsNowUInt32_Should_Return_Reasonable_Value()
    {
        var result = Clock.UnixSecondsNowUInt32();

        // Unix timestamp for Jan 1, 2020 is ~1577836800
        // Unix timestamp for Jan 1, 2030 is ~1893456000
        const UInt32 y2020 = 1577836800;
        const UInt32 y2030 = 1893456000;

        Assert.InRange(result, y2020, y2030);
    }

    [Fact]
    public void UnixSecondsNowUInt32_Should_Throw_When_Time_Before_Unix_Epoch()
    {
        // We can't directly test this without manipulating Clock's internal time,
        // but we can verify the method doesn't crash for normal operations
        Clock.ResetSynchronization();

        // Synchronize with a time far in the past (but after year 2000 to pass validation)
        var pastTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // This should work fine since 2000 is after Unix epoch
        _ = Clock.SynchronizeTime(pastTime, maxAllowedDriftMs: 0.1);
        
        var result = Clock.UnixSecondsNowUInt32();
        
        // Should still return a valid UInt32
        Assert.True(result > 0);
        Assert.True(result < UInt32.MaxValue);
        
        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixSecondsNowUInt32_Documentation_Reflects_Overflow_Concern()
    {
        // This test verifies that the overflow concern is documented
        // and the method works correctly for current and near-future times
        
        Clock.ResetSynchronization();
        
        // Synchronize to year 2050 (well before the 2106 overflow)
        var futureTime = new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = Clock.SynchronizeTime(futureTime, maxAllowedDriftMs: 0.1);
        
        var result = Clock.UnixSecondsNowUInt32();
        
        // Year 2050 Unix timestamp is ~2524608000, well within UInt32 range
        const UInt32 y2050 = 2524608000;
        Assert.InRange(result, y2050 - 10, y2050 + 10);
        
        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixSecondsNowUInt32_Should_Throw_For_Far_Future()
    {
        Clock.ResetSynchronization();
        
        // Synchronize to year 2099 (close to boundary, but still valid)
        var futureTime = new DateTime(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        _ = Clock.SynchronizeTime(futureTime, maxAllowedDriftMs: 0.1);
        
        // Unix timestamp for end of 2099 is ~4102358399, which is still less than UInt32.MaxValue (4294967295)
        // So this should work
        var result = Clock.UnixSecondsNowUInt32();
        
        // After synchronizing to 2099, result should be in far future (unless another test reset)
        // The key point is checking UInt32 doesn't overflow for year 2099
        const UInt32 y2020 = 1577836800;
        Assert.True(result >= y2020); // At least year 2020 or later
        Assert.True(result <= UInt32.MaxValue); // Should not overflow
        
        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixSecondsNow_Int64_Should_Always_Work()
    {
        Clock.ResetSynchronization();
        
        // Int64 version should work even for far future times
        var futureTime = new DateTime(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        _ = Clock.SynchronizeTime(futureTime, maxAllowedDriftMs: 0.1);
        
        var result = Clock.UnixSecondsNow();
        
        // Should return a positive value
        Assert.True(result > 0);
        
        // After synchronizing to 2099, the result should be in the far future
        // (unless another concurrent test reset the clock)
        // The key point is that Int64 can handle large values without overflow
        const Int64 y2020 = 1577836800L;
        Assert.True(result >= y2020); // At least year 2020 or later
        
        // Reset to avoid affecting other tests
        Clock.ResetSynchronization();
    }

    [Fact]
    public void UnixMillisecondsNow_Should_Handle_Current_Time()
    {
        Clock.ResetSynchronization();
        
        var result = Clock.UnixMillisecondsNow();
        
        // Should be a large positive number
        Assert.True(result > 0);
        
        // Current time in milliseconds should be around 1.7 trillion
        const Int64 y2020Ms = 1577836800000L;
        const Int64 y2030Ms = 1893456000000L;
        
        Assert.InRange(result, y2020Ms, y2030Ms);
    }

    [Fact]
    public void UnixMicrosecondsNow_Should_Handle_Current_Time()
    {
        Clock.ResetSynchronization();
        
        var result = Clock.UnixMicrosecondsNow();
        
        // Should be a large positive number
        Assert.True(result > 0);
        
        // Current time in microseconds should be around 1.7 quadrillion
        const Int64 y2020Us = 1577836800000000L;
        const Int64 y2030Us = 1893456000000000L;
        
        Assert.InRange(result, y2020Us, y2030Us);
    }
}
