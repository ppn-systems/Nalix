using Nalix.Framework.Time;
using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class TimeStampTests
{
    // helper: đợi qua tick tiếp theo của Stopwatch để đảm bảo ts2 > ts1
    private static void WaitNextTick()
    {
        Int64 t0 = Stopwatch.GetTimestamp();
        while (Stopwatch.GetTimestamp() == t0) { /* spin */ }
    }

    // helper: wait theo số tick (bận rộn, nhanh & ổn định hơn Sleep cho test ngắn)
    private static void BusyWaitTicks(Int64 waitTicks)
    {
        Int64 start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetTimestamp() - start < waitTicks) { /* spin */ }
    }

    [Fact]
    public void Now_Monotonic_Increasing_And_Comparable()
    {
        var t1 = TimeStamp.Now;
        WaitNextTick();
        var t2 = TimeStamp.Now;

        Assert.True(t2 > t1);
        Assert.True(t1 < t2);
        Assert.True(t1 != t2);
        Assert.True(t1.CompareTo(t2) < 0);
        Assert.True(t2.CompareTo(t1) > 0);

        // IEquatable + GetHashCode consistency (copy-value)
        var t1Copy = t1;
        Assert.True(t1.Equals(t1Copy));
        Assert.Equal(t1.GetHashCode(), t1Copy.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsRawValue()
    {
        var ts = TimeStamp.Now;
        String s = ts.ToString();
        Assert.Contains("TimeStamp(", s);
        Assert.Contains(ts.RawValue.ToString(), s);
    }

    [Fact]
    public void GetElapsed_Returns_Positive_And_Reasonable()
    {
        var ts = TimeStamp.Now;

        // chờ khoảng 5-10ms theo tick để đo
        Double targetMs = 8.0;
        Int64 targetTicks = (Int64)(targetMs / 1000.0 * Stopwatch.Frequency);
        BusyWaitTicks(targetTicks);

        TimeSpan elapsed = ts.GetElapsed();
        Double ms = ts.GetElapsedMilliseconds();
        Double us = ts.GetElapsedMicroseconds();

        Assert.True(elapsed.TotalMilliseconds > 0);
        Assert.True(ms > 0);
        Assert.True(us > 0);

        // các API nhất quán (cho phép sai số nhỏ)
        Assert.InRange(ms, elapsed.TotalMilliseconds - 0.5, elapsed.TotalMilliseconds + 0.5);
        Assert.InRange(us, (ms * 1000) - 1000, (ms * 1000) + 1000);
    }

    [Fact]
    public void Interval_Positive_And_Negative_And_Consistency()
    {
        var start = TimeStamp.Now;
        // đợi ~2ms
        Double targetMs = 2.0;
        Int64 ticks = (Int64)(targetMs / 1000.0 * Stopwatch.Frequency);
        BusyWaitTicks(ticks);
        var end = TimeStamp.Now;

        // dương
        TimeSpan span = TimeStamp.GetInterval(start, end);
        Double ms = TimeStamp.GetIntervalMilliseconds(start, end);
        Double us = TimeStamp.GetIntervalMicroseconds(start, end);

        Assert.True(span.TotalMilliseconds > 0);
        Assert.True(ms > 0);
        Assert.True(us > 0);

        // các API khớp nhau (tolerance nhỏ)
        Assert.InRange(ms, span.TotalMilliseconds - 0.5, span.TotalMilliseconds + 0.5);
        Assert.InRange(us, (ms * 1000) - 1500, (ms * 1000) + 1500);

        // âm (đổi thứ tự)
        TimeSpan spanNeg = TimeStamp.GetInterval(end, start);
        Double msNeg = TimeStamp.GetIntervalMilliseconds(end, start);
        Assert.True(spanNeg.TotalMilliseconds < 0);
        Assert.True(msNeg < 0);

        // đối xứng xấp xỉ
        Assert.InRange(Math.Abs(ms + msNeg), 0, 0.5);
    }

    [Fact]
    public void Sorting_Uses_CompareTo()
    {
        var a = TimeStamp.Now;
        WaitNextTick();
        var b = TimeStamp.Now;
        WaitNextTick();
        var c = TimeStamp.Now;

        var arr = new[] { c, a, b };
        var sorted = arr.OrderBy(x => x).ToArray();

        Assert.Equal(a, sorted[0]);
        Assert.Equal(b, sorted[1]);
        Assert.Equal(c, sorted[2]);
    }
}
