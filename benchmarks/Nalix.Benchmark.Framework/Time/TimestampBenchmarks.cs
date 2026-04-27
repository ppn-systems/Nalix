using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Environment;

using Nalix.Environment.Time;
namespace Nalix.Benchmark.Framework.Time;

/// <summary>
/// Benchmarks for Clock and timestamp generation performance.
/// </summary>
public class TimestampBenchmarks : NalixBenchmarkBase
{
    private DateTime _syncTargetUtc;

    [GlobalSetup]
    public void Setup()
    {
        Clock.ResetSynchronization();
        _syncTargetUtc = DateTime.UtcNow.AddMilliseconds(1500);
    }

    [IterationCleanup(Target = nameof(SynchronizeUnixMilliseconds))]
    public void CleanupSync() => Clock.ResetSynchronization();

    [Benchmark]
    public DateTime GetNowUtc() => Clock.NowUtc();

    [Benchmark]
    public long GetUnixMillisecondsNow() => Clock.UnixMillisecondsNow();

    [Benchmark]
    public long GetEpochMillisecondsNow() => Clock.EpochMillisecondsNow();

    [Benchmark]
    public long GetMonoTicksNow() => Clock.MonoTicksNow();

    [Benchmark]
    public double SynchronizeUnixMilliseconds()
        => Clock.SynchronizeUnixMilliseconds(
            new DateTimeOffset(_syncTargetUtc).ToUnixTimeMilliseconds(),
            rttMs: 25,
            maxAllowedDriftMs: 10,
            maxHardAdjustMs: 5000);
}