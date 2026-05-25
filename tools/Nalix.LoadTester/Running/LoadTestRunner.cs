// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.LoadTester.Metrics;
using Nalix.LoadTester.Reporting;
using Nalix.LoadTester.Scenarios;

namespace Nalix.LoadTester.Running;

internal sealed class LoadTestRunner
{
    private readonly LoadTestOptions _options;
    private readonly ILoadScenario _scenario;
    private readonly MetricsCollector _metrics;
    private readonly ConsoleProgressReporter _reporter;

    public LoadTestRunner(
        LoadTestOptions options,
        ILoadScenario scenario,
        MetricsCollector metrics,
        ConsoleProgressReporter reporter)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _reporter.WriteStart(_options, _scenario);

        using CancellationTokenSource duration = new(TimeSpan.FromSeconds(_options.DurationSeconds));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, duration.Token);

        Task[] workers = new Task[_options.Connections];
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (Int32 i = 0; i < workers.Length; i++)
        {
            ConnectionWorker worker = new(_options, _scenario, _metrics);
            workers[i] = worker.RunAsync(linked.Token);
        }

        Task progress = _reporter.ReportProgressAsync(
            stopwatch,
            _metrics,
            _options.ReportIntervalSeconds,
            linked.Token);

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        stopwatch.Stop();

        try
        {
            await progress.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        LoadTestReport report = _metrics.CreateReport(stopwatch.Elapsed);
        _reporter.WriteFinal(report);
    }
}
