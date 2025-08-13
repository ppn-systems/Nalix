using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class Clock_FormattingTests
{
    [Fact]
    public void FormatTimeSpan_Zero() => Assert.Equal("0s", Clock.FormatTimeSpan(TimeSpan.Zero));

    [Fact]
    public void FormatTimeSpan_Parts_NoMs()
    {
        var ts = new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4);
        var s = Clock.FormatTimeSpan(ts);
        Assert.Contains("1d", s);
        Assert.Contains("2h", s);
        Assert.Contains("3m", s);
        Assert.Contains("4s", s);
        Assert.DoesNotContain("ms", s);
    }

    [Fact]
    public void FormatTimeSpan_WithMs()
    {
        var ts = new TimeSpan(0, 0, 0).Add(TimeSpan.FromMilliseconds(150));
        var s = Clock.FormatTimeSpan(ts, includeMilliseconds: true);
        Assert.Contains("150ms", s);
    }

    [Fact]
    public void FormatElapsedTime_Requires_Utc()
    {
        var local = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);
        _ = Assert.Throws<ArgumentException>(() => Clock.FormatElapsedTime(local));
    }

    [Fact]
    public void FormatElapsedTime_Returns_String()
    {
        var start = Clock.GetUtcNowPrecise().AddMilliseconds(-1234);
        var s = Clock.FormatElapsedTime(start, includeMilliseconds: true);
        // chỉ cần có s / ms
        Assert.Contains("s", s);
    }
}
