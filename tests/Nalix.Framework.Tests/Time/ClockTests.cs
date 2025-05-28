using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class ClockTests
{
    [Fact]
    public void UnixSecondsNow_Should_Match()
    {
        var now = DateTime.UtcNow;
        var unix = Clock.UnixSecondsNow();
        var expected = (Int64)(now - DateTime.UnixEpoch).TotalSeconds;
        Assert.InRange(unix, expected - 1, expected + 1);
    }

    [Fact]
    public void UnixMillisecondsNow_Should_Match_RawUtcWindow()
    {
        var unix = Clock.UnixMillisecondsNow();
        var now = DateTime.UtcNow;
        var expected = (Int64)(now - DateTime.UnixEpoch).TotalMilliseconds;

        // 2.5s đủ an toàn cho các môi trường có NTP/VM jitter
        Assert.InRange(unix, expected - 2500, expected + 2500);
    }

    [Fact]
    public void UnixMicrosecondsNow_Should_Match()
    {
        var now = DateTime.UtcNow;
        var unix = Clock.UnixMicrosecondsNow();
        var expected = (now - DateTime.UnixEpoch).Ticks / 10;
        Assert.InRange(unix, expected - 500, expected + 2500); // Sai số microsecond
    }

    [Fact]
    public void UnixTicksNow_Should_Match()
    {
        var now = DateTime.UtcNow;
        var unix = Clock.UnixTicksNow();
        var expected = (now - DateTime.UnixEpoch).Ticks;
        Assert.InRange(unix, expected - 10000, expected + 10000);
    }

    [Fact]
    public void UnixTimeSecondsToDateTime_Should_Convert()
    {
        const Int64 unix = 1700000000L;
        var dt = Clock.UnixTimeSecondsToDateTime(unix);
        Assert.Equal(unix, (Int64)(dt - DateTime.UnixEpoch).TotalSeconds);
    }

    [Fact]
    public void UnixTimeMillisecondsToDateTime_Should_Convert()
    {
        const Int64 unix = 1700000000000L;
        var dt = Clock.UnixTimeMillisecondsToDateTime(unix);
        Assert.Equal(unix, (Int64)(dt - DateTime.UnixEpoch).TotalMilliseconds);
    }

    [Fact]
    public void UnixTimeMicrosecondsToDateTime_Should_Convert()
    {
        const Int64 micro = 170000000000000L;
        var dt = Clock.UnixTimeMicrosecondsToDateTime(micro);
        Assert.Equal(micro, (dt - DateTime.UnixEpoch).Ticks / 10);
    }

    [Fact]
    public void TimeMillisecondsToDateTime_Should_Convert()
    {
        const Int64 ms = 1000000;
        var dt = Clock.TimeMillisecondsToDateTime(ms);
        Assert.Equal(ms, (Int64)(dt - DateTime.UnixEpoch.AddSeconds(Clock.TimeEpochTimestamp)).TotalMilliseconds);
    }

    [Fact]
    public void UnixTimeToDateTime_And_DateTimeToUnixTime_Should_Convert()
    {
        var now = DateTime.UtcNow;
        var span = now - DateTime.UnixEpoch;
        var dt2 = Clock.UnixTimeToDateTime(span);
        Assert.Equal(now.Ticks / TimeSpan.TicksPerSecond, dt2.Ticks / TimeSpan.TicksPerSecond);

        var unix = Clock.DateTimeToUnixTime(now);
        Assert.Equal(span.Ticks, unix.Ticks);
    }

    [Fact]
    public void DateTimeToTime_Should_Throw_IfNotUtc()
    {
        var dt = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Local);
        _ = Assert.Throws<ArgumentException>(() => Clock.DateTimeToTime(dt));
    }

    [Fact]
    public void DateTimeToUnixTimeSeconds_And_Milliseconds_Should_Throw_IfNotUtc()
    {
        var dt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Local);
        _ = Assert.Throws<ArgumentException>(() => Clock.DateTimeToUnixTimeSeconds(dt));
        _ = Assert.Throws<ArgumentException>(() => Clock.DateTimeToUnixTimeMilliseconds(dt));
    }

    [Fact]
    public void SynchronizeTime_And_ResetSynchronization()
    {
        var now = DateTime.UtcNow.AddSeconds(1);
        var drift = Clock.SynchronizeTime(now, 0.1);
        Assert.True(Math.Abs(drift) > 0);
        Assert.True(Clock.IsSynchronized);
        Assert.True(Clock.LastSyncTime == now);

        Clock.ResetSynchronization();
        Assert.False(Clock.IsSynchronized);
        Assert.Equal(DateTime.MinValue, Clock.LastSyncTime);
    }

    [Fact]
    public void GetCurrentErrorEstimateMs_Should_Return_Zero_IfNotSync()
    {
        Clock.ResetSynchronization();
        Assert.Equal(0, Clock.GetCurrentErrorEstimateMs());
    }
}