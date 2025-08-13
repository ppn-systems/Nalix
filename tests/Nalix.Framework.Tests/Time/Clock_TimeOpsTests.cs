using Nalix.Framework.Time;
using System;
using System.Threading;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class Clock_TimeOpsTests
{
    [Fact]
    public void WaitUntil_Reaches_Target()
    {
        var target = Clock.GetUtcNowPrecise().AddMilliseconds(30);
        Boolean ok = Clock.WaitUntil(target);
        Assert.True(ok);
        Assert.True(Clock.GetUtcNowPrecise() >= target);
    }

    [Fact]
    public void WaitUntil_Can_Be_Cancelled()
    {
        using var cts = new CancellationTokenSource();
        var far = Clock.GetUtcNowPrecise().AddSeconds(2);

        // cancel sớm
        cts.CancelAfter(50);
        Boolean ok = Clock.WaitUntil(far, cts.Token);
        Assert.False(ok);
    }

    [Fact]
    public void IsTimeBetween_Works()
    {
        var now = Clock.GetUtcNowPrecise();
        var start = now.AddMilliseconds(-50);
        var end = now.AddMilliseconds(50);
        Assert.True(Clock.IsTimeBetween(start, end));

        Assert.False(Clock.IsTimeBetween(end, end.AddMilliseconds(10)));
    }

    [Fact]
    public void GetTimeRemaining_Positive_And_Zero()
    {
        var future = Clock.GetUtcNowPrecise().AddMilliseconds(20);
        var rem1 = Clock.GetTimeRemaining(future);
        Assert.True(rem1 > TimeSpan.Zero);

        var past = Clock.GetUtcNowPrecise().AddMilliseconds(-20);
        var rem2 = Clock.GetTimeRemaining(past);
        Assert.Equal(TimeSpan.Zero, rem2);
    }

    [Fact]
    public void HasElapsed_Returns_True_When_Duration_Passed()
    {
        var start = Clock.GetUtcNowPrecise().AddMilliseconds(-100);
        Assert.True(Clock.HasElapsed(start, TimeSpan.FromMilliseconds(50)));
        Assert.False(Clock.HasElapsed(start, TimeSpan.FromSeconds(10))); // chưa đủ nếu start ~now?
    }

    [Fact]
    public void GetPercentageComplete_Edges_And_Mid()
    {
        var now = Clock.GetUtcNowPrecise();

        // start >= end => 1.0
        Assert.Equal(1.0, Clock.GetPercentageComplete(now, now));

        // trước start => 0
        var start = now.AddMilliseconds(50);
        var end = start.AddMilliseconds(100);
        Assert.Equal(0.0, Clock.GetPercentageComplete(start, end), 10);

        // giữa khoảng => (0,1)
        start = now.AddMilliseconds(-50);
        end = now.AddMilliseconds(50);
        Double p = Clock.GetPercentageComplete(start, end);
        Assert.InRange(p, 0.0, 1.0);
    }
}
