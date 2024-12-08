using Nalix.Framework.Time;
using System;
using System.Diagnostics;
using System.Threading;
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
    public void GetUtcNowPrecise_Is_Monotonicish_And_Close_To_UtcNow()
    {
        var t1 = Clock.GetUtcNowPrecise();
        Thread.Sleep(2);
        var t2 = Clock.GetUtcNowPrecise();

        Assert.True(t2 > t1);

        // So với RawUtcNow: chênh không quá 500ms trong đa số môi trường
        var diffToRawMs = Math.Abs((t2 - Clock.GetRawUtcNow()).TotalMilliseconds);
        Assert.InRange(diffToRawMs, 0, 500);

        // So với DateTime.UtcNow: nới lên 2500ms để tránh flaky trên CI có NTP step
        var diffMs = Math.Abs((t2 - DateTime.UtcNow).TotalMilliseconds);
        Assert.InRange(diffMs, 0, 2500);
    }

    [Fact]
    public void GetUtcNowString_DefaultFormat()
    {
        String s = Clock.GetUtcNowString(); // yyyy-MM-dd HH:mm:ss.fff
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}", s);
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
        Assert.True(ticks2 - ticks1 > 0);
    }

    [Fact]
    public void UnixTime_And_ApplicationTime_Positive()
    {
        var ut = Clock.UnixTime();
        var app = Clock.ApplicationTime();

        Assert.True(ut > TimeSpan.Zero);
        Assert.True(app > TimeSpan.Zero);

        // app ~= now - TimeEpoch
        var approx = Clock.GetUtcNowPrecise() - Clock.TimeEpochDatetime;
        Assert.InRange(Math.Abs((approx - app).TotalMilliseconds), 0, 50);
    }

    [Fact]
    public void GetRawUtcNow_Is_UtcNow()
    {
        var raw = Clock.GetRawUtcNow();
        Assert.Equal(DateTimeKind.Utc, raw.Kind);
        Assert.InRange(Math.Abs((raw - DateTime.UtcNow).TotalMilliseconds), 0, 50);
    }
}
