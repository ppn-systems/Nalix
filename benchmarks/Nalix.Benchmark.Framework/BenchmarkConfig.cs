using System;
using System.Globalization;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
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
        string artifactsPath = Path.Combine(Environment.CurrentDirectory, "BenchmarkDotNet.Artifacts", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        this.Add(DefaultConfig.Instance);
        _ = this.AddJob(
             Job.Default
                .WithRuntime(CoreRuntime.Core10_0) // .NET 10
                .WithAffinity(new IntPtr(1))
                .WithPowerPlan(PowerPlan.HighPerformance)
                .WithLaunchCount(1)
                .WithWarmupCount(6)
                .WithIterationCount(10)
                .WithMinIterationTime(TimeInterval.FromMilliseconds(250))
                .WithGcServer(true)
                .WithStrategy(RunStrategy.Throughput)
                .WithId("Net10"));

        _ = this.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());
        _ = this.AddColumn(StatisticColumn.P95);
        _ = this.AddColumn(RankColumn.Arabic);
        _ = this.AddColumn(CategoriesColumn.Default);
        _ = this.AddExporter(MarkdownExporter.GitHub);
        _ = this.AddDiagnoser(MemoryDiagnoser.Default);
        _ = this.WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        _ = this.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(32));
        _ = this.AddLogicalGroupRule(BenchmarkDotNet.Configs.LogicalGroupRule.ByCategory);

        _ = this.WithArtifactsPath(artifactsPath)
                .WithOption(ConfigOptions.DisableLogFile, true)
                .WithOption(ConfigOptions.JoinSummary, false)
                .WithWakeLock(WakeLockType.None);
    }
}
