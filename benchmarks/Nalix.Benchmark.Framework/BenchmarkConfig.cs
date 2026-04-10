using System;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
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
             Job.Default
                .WithRuntime(CoreRuntime.Core10_0) // .NET 10
                .WithLaunchCount(2)
                .WithWarmupCount(10)
                .WithIterationCount(20)
                .WithMinIterationTime(TimeInterval.FromMilliseconds(250))
                .WithId("Net10"));

        _ = this.AddColumnProvider(DefaultColumnProviders.Instance);
        _ = this.AddColumn(StatisticColumn.P95);
        _ = this.AddExporter(MarkdownExporter.GitHub);
        _ = this.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(32));
        _ = this.WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));

        _ = this.WithArtifactsPath(artifactsPath)
                .WithOption(ConfigOptions.DisableLogFile, true)
                .WithOption(ConfigOptions.JoinSummary, true)
                .WithWakeLock(WakeLockType.None);
    }
}
