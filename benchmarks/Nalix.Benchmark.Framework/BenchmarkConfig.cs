using System;
using System.IO;
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
using Perfolizer.Mathematics.OutlierDetection;

namespace Nalix.Benchmark.Framework;

public sealed class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        string artifactsPath = Path.Combine(Environment.CurrentDirectory, "BenchmarkDotNet.Artifacts");

        this.Add(DefaultConfig.Instance);
        _ = this.AddJob(
             Job.Default
                .WithRuntime(CoreRuntime.Core10_0) // .NET 10
                                                   // Không pin vào 1 core để tránh nhiễu bất thường trên đúng 1 logical core
                                                   // .WithAffinity(new IntPtr(1))
                .WithPowerPlan(PowerPlan.HighPerformance)
                .WithLaunchCount(3)
                .WithWarmupCount(10)
                .WithIterationCount(15)
                .WithMinIterationTime(TimeInterval.FromMilliseconds(500))
                .WithGcServer(true)
                .WithStrategy(RunStrategy.Throughput)
                .WithOutlierMode(OutlierMode.RemoveUpper)
                .WithId("Net10"));

        _ = this.AddColumn(
            StatisticColumn.Min,
            StatisticColumn.Mean,
            StatisticColumn.Median,
            StatisticColumn.P95,
            StatisticColumn.Max,
            StatisticColumn.StdDev,
            RankColumn.Arabic,
            CategoriesColumn.Default);

        _ = this.AddExporter(MarkdownExporter.GitHub);

        _ = this.AddDiagnoser(MemoryDiagnoser.Default);

        _ = this.WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        _ = this.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(32));

        _ = this.AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByCategory);

        _ = this.WithArtifactsPath(artifactsPath)
                .WithOption(ConfigOptions.DisableLogFile, true)
                .WithOption(ConfigOptions.JoinSummary, false)
                .WithWakeLock(WakeLockType.System);
    }
}
