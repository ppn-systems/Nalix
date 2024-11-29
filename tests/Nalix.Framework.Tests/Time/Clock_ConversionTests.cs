using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class Clock_ConversionTests
{
    [Fact]
    public void UnixSeconds_To_DateTime()
    {
        var dt0 = Clock.UnixTimeSecondsToDateTime(0);
        Assert.Equal(DateTime.UnixEpoch, dt0);

        var dt = Clock.UnixTimeSecondsToDateTime(Clock.TimeEpochTimestamp);
        Assert.Equal(Clock.TimeEpochDatetime, dt);
    }

    [Fact]
    public void UnixMilliseconds_To_DateTime()
    {
        var ts = 1234567890L;
        var dt = Clock.UnixTimeMillisecondsToDateTime(ts);
        Assert.Equal(DateTime.UnixEpoch.AddMilliseconds(ts), dt);
    }

    [Fact]
    public void UnixMicroseconds_To_DateTime()
    {
        Int64 us = 1234567;
        var dt = Clock.UnixTimeMicrosecondsToDateTime(us);
        Assert.Equal(DateTime.UnixEpoch.AddTicks(us * 10), dt);
    }

    [Fact]
    public void TimeMilliseconds_To_DateTime()
    {
        Int64 ms = 42_000;
        var dt = Clock.TimeMillisecondsToDateTime(ms);
        Assert.Equal(Clock.TimeEpochDatetime.AddMilliseconds(ms), dt);
    }

    [Fact]
    public void UnixTime_To_DateTime_And_Reverse()
    {
        var span = TimeSpan.FromSeconds(10);
        var dt = Clock.UnixTimeToDateTime(span);
        Assert.Equal(DateTime.UnixEpoch.AddSeconds(10), dt);

        _ = Assert.Throws<ArgumentException>(() => Clock.UnixTimeToDateTime(TimeSpan.FromSeconds(-1)));

        var utc = new DateTime(2021, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var back = Clock.DateTimeToUnixTime(utc);
        Assert.Equal(utc - DateTime.UnixEpoch, back);

        // Kind check
        var local = new DateTime(2021, 5, 1, 0, 0, 0, DateTimeKind.Local);
        _ = Assert.Throws<ArgumentException>(() => Clock.DateTimeToUnixTime(local));
    }

    [Fact]
    public void DateTime_To_Time_And_UnixSeconds_Milliseconds()
    {
        var utc = new DateTime(2020, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        var time = Clock.DateTimeToTime(utc);
        Assert.Equal(TimeSpan.FromHours(1), time);

        _ = Assert.Throws<ArgumentException>(() => Clock.DateTimeToTime(DateTime.SpecifyKind(utc, DateTimeKind.Local)));

        UInt64 secs = Clock.DateTimeToUnixTimeSeconds(utc);
        UInt64 ms = Clock.DateTimeToUnixTimeMilliseconds(utc);
        Assert.Equal((UInt64)(utc - DateTime.UnixEpoch).TotalSeconds, secs);
        Assert.Equal((UInt64)(utc - DateTime.UnixEpoch).TotalMilliseconds, ms);

        _ = Assert.Throws<ArgumentException>(() => Clock.DateTimeToUnixTimeSeconds(DateTime.SpecifyKind(utc, DateTimeKind.Local)));
        _ = Assert.Throws<ArgumentException>(() => Clock.DateTimeToUnixTimeMilliseconds(DateTime.SpecifyKind(utc, DateTimeKind.Local)));
    }
}
