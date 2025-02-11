// Copyright (c) 2025 PPN Corporation. All rights reserved.

#nullable enable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nalix.Common.Logging;
using Nalix.Logging.Core;

namespace Nalix.Logging.Benchmarks;

/// <summary>
/// Entry point for Nalix.Logging benchmarks.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        _ = BenchmarkRunner.Run<FormatterBenchmarks>();
        _ = BenchmarkRunner.Run<DistributorBenchmarks>();
        _ = BenchmarkRunner.Run<BatchingBenchmarks>();
    }
}

/// <summary>
/// Benchmarks for log formatter performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 100, warmupCount: 10)]
public class FormatterBenchmarks
{
    private NLogixFormatter? _formatter;
    private LogEntry _simpleEntry;
    private LogEntry _complexEntry;
    private LogEntry _exceptionEntry;

    [GlobalSetup]
    public void Setup()
    {
        _formatter = new NLogixFormatter(colors: false);

        _simpleEntry = new LogEntry(
            LogLevel.Information,
            new EventId(1, "Test"),
            "Simple log message",
            null);

        _complexEntry = new LogEntry(
            LogLevel.Warning,
            new EventId(100, "ComplexEvent"),
            "Complex log message with lots of details about what happened in the system",
            null);

        try
        {
            throw new System.InvalidOperationException("Test exception");
        }
        catch (System.Exception ex)
        {
            _exceptionEntry = new LogEntry(
                LogLevel.Error,
                new EventId(500, "Error"),
                "An error occurred",
                ex);
        }
    }

    [Benchmark(Description = "Format Simple Log")]
    public string FormatSimpleLog()
    {
        return _formatter!.Format(_simpleEntry);
    }

    [Benchmark(Description = "Format Complex Log")]
    public string FormatComplexLog()
    {
        return _formatter!.Format(_complexEntry);
    }

    [Benchmark(Description = "Format Log with Exception")]
    public string FormatExceptionLog()
    {
        return _formatter!.Format(_exceptionEntry);
    }
}

/// <summary>
/// Benchmarks for log distributor performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 100, warmupCount: 10)]
public class DistributorBenchmarks
{
    private NLogixDistributor? _distributor;
    private NullLogTarget? _target;
    private LogEntry _entry;

    [GlobalSetup]
    public void Setup()
    {
        _distributor = new NLogixDistributor();
        _target = new NullLogTarget();
        _ = _distributor.RegisterTarget(_target);

        _entry = new LogEntry(
            LogLevel.Information,
            new EventId(1, "Test"),
            "Benchmark log message",
            null);
    }

    [Benchmark(Description = "Publish to Single Target")]
    public void PublishSingleTarget()
    {
        _distributor!.Publish(_entry);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _distributor?.Dispose();
    }
}

/// <summary>
/// Benchmarks for batching performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 100, warmupCount: 10)]
public class BatchingBenchmarks
{
    private LogEntry[]? _entries;

    [Params(10, 100, 1000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _entries = new LogEntry[EntryCount];
        for (int i = 0; i < EntryCount; i++)
        {
            _entries[i] = new LogEntry(
                LogLevel.Information,
                new EventId(i, $"Event{i}"),
                $"Log message number {i}",
                null);
        }
    }

    [Benchmark(Description = "Format Batch")]
    public void FormatBatch()
    {
        var formatter = new NLogixFormatter(colors: false);
        for (int i = 0; i < _entries!.Length; i++)
        {
            _ = formatter.Format(_entries[i]);
        }
    }
}

/// <summary>
/// Null log target for benchmarking without I/O overhead.
/// </summary>
internal sealed class NullLogTarget : ILoggerTarget
{
    public void Publish(LogEntry logMessage)
    {
        // Do nothing - this is for benchmarking the distribution logic
    }
}
