using Nalix.Framework.Time;
using System;
using System.Diagnostics;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public class Clock_PerformanceTests
{
    private static void BusyWaitMs(Double ms)
    {
        Int64 ticks = (Int64)(ms / 1000.0 * Stopwatch.Frequency);
        Int64 start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetTimestamp() - start < ticks) { }
    }

    [Fact]
    public void Start_GetElapsed_Methods_Work()
    {
        Clock.StartMeasurement();
        BusyWaitMs(5);
        var ts = Clock.GetElapsed();
        var ms = Clock.GetElapsedMilliseconds();
        var us = Clock.GetElapsedMicroseconds();

        Assert.True(ts.TotalMilliseconds >= 1);
        Assert.InRange(ms, ts.TotalMilliseconds - 0.5, ts.TotalMilliseconds + 0.5);
        Assert.InRange(us, (ms * 1000) - 1500, (ms * 1000) + 1500);
    }

    [Fact]
    public void MeasureExecutionTime_Runs_Action_And_Returns_Elapsed()
    {
        Double elapsed = Clock.MeasureExecutionTime(() => BusyWaitMs(3));
        Assert.InRange(elapsed, 2, 1000);
    }

    [Fact]
    public void MeasureFunction_Returns_Result_And_Elapsed()
    {
        var (result, elapsed) = Clock.MeasureFunction(() =>
        {
            BusyWaitMs(2);
            return 123;
        });

        Assert.Equal(123, result);
        Assert.InRange(elapsed, 1, 1000);
    }
}
