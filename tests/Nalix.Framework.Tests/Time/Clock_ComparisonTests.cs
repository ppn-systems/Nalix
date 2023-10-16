using Nalix.Framework.Time;
using System;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class Clock_ComparisonTests
{
    [Fact]
    public void Max_Min_TimeSpan()
    {
        var a = TimeSpan.FromSeconds(1);
        var b = TimeSpan.FromSeconds(2);
        Assert.Equal(b, Clock.Max(a, b));
        Assert.Equal(a, Clock.Min(a, b));
    }

    [Fact]
    public void Clamp_DateTime_And_TimeSpan()
    {
        var min = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var max = new DateTime(2021, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(min, Clock.Clamp(min.AddSeconds(-1), min, max));
        Assert.Equal(max, Clock.Clamp(max.AddSeconds(1), min, max));

        var mid = min.AddHours(1);
        Assert.Equal(mid, Clock.Clamp(mid, min, max));

        var tsMin = TimeSpan.FromSeconds(1);
        var tsMax = TimeSpan.FromSeconds(5);
        Assert.Equal(tsMin, Clock.Clamp(TimeSpan.Zero, tsMin, tsMax));
        Assert.Equal(tsMax, Clock.Clamp(TimeSpan.FromSeconds(10), tsMin, tsMax));
        Assert.Equal(TimeSpan.FromSeconds(3), Clock.Clamp(TimeSpan.FromSeconds(3), tsMin, tsMax));
    }

    [Fact]
    public void IsInRange_Symmetric()
    {
        var now = Clock.GetUtcNowPrecise();
        var within = now.AddMilliseconds(-100);
        Assert.True(Clock.IsInRange(within, TimeSpan.FromMilliseconds(200)));

        var outOf = now.AddSeconds(-5);
        Assert.False(Clock.IsInRange(outOf, TimeSpan.FromMilliseconds(200)));
    }
}
