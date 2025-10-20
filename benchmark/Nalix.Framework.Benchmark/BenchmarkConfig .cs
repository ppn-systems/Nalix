// Copyright (c) 2025 PPN Corporation.
// All rights reserved.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Reports;

namespace Nalix.Framework.Benchmark;

/// <summary>
/// Provides a common configuration for all benchmark classes.
/// Enables detailed diagnostics and GC information for .NET 9.
/// </summary>
public sealed class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // Add runtime job for .NET 9.0
        AddJob(Job
            .Default
            .WithRuntime(CoreRuntime.Core90)
            .WithId(".NET 9.0")
            .WithWarmupCount(3)
            .WithIterationCount(10)
            .WithLaunchCount(1)
            .WithGcForce(true)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithMaxIterationCount(20)
        );

        // Add standard columns
        AddColumn(TargetMethodColumn.Method,
                  StatisticColumn.Mean,
                  StatisticColumn.Median,
                  StatisticColumn.StdDev,
                  StatisticColumn.P95,
                  StatisticColumn.Min,
                  StatisticColumn.Max);

        // Add loggers and exporters
        AddLogger(ConsoleLogger.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);
        AddExporter(HtmlExporter.Default);

        // Add diagnosers (GC, memory)
        AddDiagnoser(MemoryDiagnoser.Default);

        // Add analyzers for hints/warnings
        AddAnalyser(EnvironmentAnalyser.Default);

        // Set summary style
        SummaryStyle = SummaryStyle.Default.WithMaxParameterColumnWidth(50);
    }
}
