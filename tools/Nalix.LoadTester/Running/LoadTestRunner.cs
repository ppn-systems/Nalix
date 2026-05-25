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

        WorkloadState state = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        using WorkerPool workers = new(_options, _scenario, _metrics, state, cancellationToken);
        using CancellationTokenSource progressCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Task progress = _reporter.ReportProgressAsync(
            stopwatch,
            _metrics,
            state,
            _options.Connections,
            _options.ReportIntervalSeconds,
            progressCancellation.Token);

        try
        {
            await RunRampUpAsync(workers, state, cancellationToken).ConfigureAwait(false);
            await RunDelayPhaseAsync(state, WorkloadPhase.Warmup, _options.WarmupSeconds, cancellationToken).ConfigureAwait(false);

            state.Phase = WorkloadPhase.Steady;
            _metrics.StartMeasurement();
            await Task.Delay(TimeSpan.FromSeconds(_options.DurationSeconds), cancellationToken).ConfigureAwait(false);
            _metrics.StopMeasurement();

            state.Phase = WorkloadPhase.Cooldown;
            await workers.StopWorkersGraduallyAsync(TimeSpan.FromSeconds(_options.CooldownSeconds), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _metrics.StopMeasurement();
            state.Phase = WorkloadPhase.Completed;
            workers.StopAll();
            progressCancellation.Cancel();
        }

        try
        {
            await workers.WaitAsync().ConfigureAwait(false);
            await progress.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        stopwatch.Stop();

        LoadTestReport report = _metrics.CreateReport(
            stopwatch.Elapsed,
            _metrics.MeasuredElapsed);
        _reporter.WriteFinal(report);

        if (!String.IsNullOrWhiteSpace(_options.OutputPath))
        {
            await ReportExporter.ExportAsync(
                _options.OutputPath,
                _options,
                _scenario,
                report,
                cancellationToken).ConfigureAwait(false);

            _reporter.WriteExported(_options.OutputPath);
        }
    }

    private async Task RunRampUpAsync(WorkerPool workers, WorkloadState state, CancellationToken cancellationToken)
    {
        state.Phase = WorkloadPhase.RampUp;

        if (_options.RampUpSeconds == 0)
        {
            workers.StartWorkers(_options.Connections);
            return;
        }

        workers.StartWorkers(_options.StartConnections);

        Int32 remaining = _options.Connections - _options.StartConnections;
        if (remaining <= 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.RampUpSeconds), cancellationToken).ConfigureAwait(false);
            return;
        }

        TimeSpan delay = TimeSpan.FromTicks(Math.Max(1, TimeSpan.FromSeconds(_options.RampUpSeconds).Ticks / remaining));
        for (Int32 i = 0; i < remaining; i++)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            workers.StartWorkers(1);
        }
    }

    private static async Task RunDelayPhaseAsync(
        WorkloadState state,
        WorkloadPhase phase,
        Int32 seconds,
        CancellationToken cancellationToken)
    {
        state.Phase = phase;
        if (seconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
        }
    }
}
