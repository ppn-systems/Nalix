using Nalix.Framework.Time;
using System;
using System.Threading;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class ClockTests
{
    [Fact]
    public void GetUtcNowPrecise_Should_BeCloseToUtcNow()
    {
        var now = DateTime.UtcNow;
        var precise = Clock.GetUtcNowPrecise();
        var diff = Math.Abs((precise - now).TotalMilliseconds);
        Assert.True(diff < 100, $"Lệch thời gian quá lớn: {diff}ms");
    }

    [Fact]
    public void GetUtcNowString_Should_Return_CorrectFormat()
    {
        var s = Clock.GetUtcNowString("yyyy-MM-dd HH:mm");
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}", s);
    }

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
    public void Max_Min_TimeSpan()
    {
        var t1 = TimeSpan.FromSeconds(10);
        var t2 = TimeSpan.FromSeconds(20);
        Assert.Equal(t2, Clock.Max(t1, t2));
        Assert.Equal(t1, Clock.Min(t1, t2));
    }

    [Fact]
    public void IsInRange_Should_Work()
    {
        var now = Clock.GetUtcNowPrecise();
        Assert.True(Clock.IsInRange(now, TimeSpan.FromSeconds(1)));
        Assert.False(Clock.IsInRange(now.AddSeconds(10), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Clamp_DateTime_And_TimeSpan()
    {
        var min = DateTime.UtcNow.AddSeconds(-1);
        var max = DateTime.UtcNow.AddSeconds(1);
        var value = DateTime.UtcNow.AddSeconds(5);
        Assert.Equal(max, Clock.Clamp(value, min, max));
        Assert.Equal(min, Clock.Clamp(min.AddSeconds(-5), min, max));

        var minTs = TimeSpan.FromSeconds(1);
        var maxTs = TimeSpan.FromSeconds(5);
        var ts = TimeSpan.FromSeconds(10);
        Assert.Equal(maxTs, Clock.Clamp(ts, minTs, maxTs));
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