using System;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Nalix.Benchmark.Framework;

public sealed class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        string artifactsPath = Path.Combine(
            Environment.CurrentDirectory,
            "BenchmarkDotNet.Artifacts",
            "runs",
            DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

        this.Add(DefaultConfig.Instance);
        this.AddJob(
            Job
                .ShortRun
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(8)
                .WithId("ShortRun"));

        _ = this
            .WithArtifactsPath(artifactsPath)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
            .WithOption(ConfigOptions.DisableLogFile, true)
            .WithOption(ConfigOptions.JoinSummary, true)
            .WithWakeLock(WakeLockType.None);
    }
}
