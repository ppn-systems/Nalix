using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Environment.Time;

namespace Nalix.Benchmark.Framework.Time;

/// <summary>
/// Benchmarks for Clock and timestamp generation performance.
/// </summary>
public class TimestampBenchmarks : NalixBenchmarkBase
{
    private long _monoTickDelta;

    [GlobalSetup]
    public void Setup() => _monoTickDelta = Clock.TicksPerSecond / 2;

    [Benchmark]
    public DateTime GetNowUtc() => Clock.NowUtc();

    [Benchmark]
    public long GetUnixSecondsNow() => Clock.UnixSecondsNow();

    [Benchmark]
    public long GetUnixMillisecondsNow() => Clock.UnixMillisecondsNow();

    [Benchmark]
    public long GetUnixMicrosecondsNow() => Clock.UnixMicrosecondsNow();

    [Benchmark]
    public long GetUnixTicksNow() => Clock.UnixTicksNow();

    [Benchmark]
    public long GetEpochMillisecondsNow() => Clock.EpochMillisecondsNow();

    [Benchmark]
    public long GetMonoTicksNow() => Clock.MonoTicksNow();

    [Benchmark]
    public double ConvertMonoTicksToMilliseconds() => Clock.MonoTicksToMilliseconds(_monoTickDelta);
}
