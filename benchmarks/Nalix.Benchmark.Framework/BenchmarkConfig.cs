using System;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;

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
        _ = this.AddJob(
             Job.ShortRun
                .WithRuntime(CoreRuntime.Core10_0) // .NET 10
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(8)
                .WithMinIterationTime(TimeInterval.FromMilliseconds(50))
                .WithId("ShortRun"));

        _ = this
            .WithArtifactsPath(artifactsPath)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
            .WithOption(ConfigOptions.DisableLogFile, true)
            .WithOption(ConfigOptions.JoinSummary, true)
            .WithWakeLock(WakeLockType.None);
    }
}
