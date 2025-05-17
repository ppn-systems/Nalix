using Nalix.Framework.Time;
using System;
using System.Diagnostics;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class Clock_BasicTests
{
    private static void BusyWaitMs(Double ms)
    {
        Int64 ticks = (Int64)(ms / 1000.0 * Stopwatch.Frequency);
        Int64 start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetTimestamp() - start < ticks) { }
    }

    [Fact]
    public void Static_Constants_And_Props_Are_Consistent()
    {
        Assert.True(Clock.IsHighResolution == Stopwatch.IsHighResolution);
        Assert.Equal(Stopwatch.Frequency, Clock.TicksPerSecond);
        Assert.Equal(1.0 / Stopwatch.Frequency, Clock.TickFrequency, 8);

        Assert.Equal(1577836800L, Clock.TimeEpochTimestamp);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), Clock.TimeEpochDatetime);
    }

    [Fact]
    public void Unix_Time_Getters_Increase_And_Agree()
    {
        Int64 s1 = Clock.UnixSecondsNow();
        Int64 ms1 = Clock.UnixMillisecondsNow();
        Int64 us1 = Clock.UnixMicrosecondsNow();
        Int64 ticks1 = Clock.UnixTicksNow();

        BusyWaitMs(3);

        Int64 s2 = Clock.UnixSecondsNow();
        Int64 ms2 = Clock.UnixMillisecondsNow();
        Int64 us2 = Clock.UnixMicrosecondsNow();
        Int64 ticks2 = Clock.UnixTicksNow();

        Assert.True(s2 >= s1);
        Assert.True(ms2 > ms1);
        Assert.True(us2 > us1);
        Assert.True(ticks2 > ticks1);

        // Quan hệ gần đúng
        Assert.InRange(ms2 - ms1, 2, 1000);
        Assert.InRange(us2 - us1, 2000, 1_000_000);
        Assert.True(ticks2 > ticks1);
    }
}
