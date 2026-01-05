using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

[Collection("ClockTests")]
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
}