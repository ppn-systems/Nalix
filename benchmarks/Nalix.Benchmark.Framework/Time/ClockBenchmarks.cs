using System;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.Time;

namespace Nalix.Benchmark.Framework.Time;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class ClockBenchmarks
{
    private DateTime _syncTargetUtc;

    [GlobalSetup]
    public void Setup()
    {
        Clock.ResetSynchronization();
        _syncTargetUtc = DateTime.UtcNow.AddMilliseconds(1500);
    }

    [IterationCleanup(Target = nameof(SynchronizeUnixMilliseconds_WithRtt))]
    public void CleanupSync() => Clock.ResetSynchronization();

    [Benchmark]
    public DateTime NowUtc()
        => Clock.NowUtc();

    [Benchmark]
    public long UnixMillisecondsNow()
        => Clock.UnixMillisecondsNow();

    [Benchmark]
    public long EpochMillisecondsNow()
        => Clock.EpochMillisecondsNow();

    [Benchmark]
    public long MonoTicksNow()
        => Clock.MonoTicksNow();

    [Benchmark]
    public double SynchronizeUnixMilliseconds_WithRtt()
        => Clock.SynchronizeUnixMilliseconds(
            new DateTimeOffset(_syncTargetUtc).ToUnixTimeMilliseconds(),
            rttMs: 25,
            maxAllowedDriftMs: 10,
            maxHardAdjustMs: 5000);
}
