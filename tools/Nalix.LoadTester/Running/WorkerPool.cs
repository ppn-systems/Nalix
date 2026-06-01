// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.LoadTester.Metrics;
using Nalix.LoadTester.Scenarios;

namespace Nalix.LoadTester.Running;

internal sealed class WorkerPool : IDisposable
{
    private readonly LoadTestOptions _options;
    private readonly ILoadScenario _scenario;
    private readonly MetricsCollector _metrics;
    private readonly WorkloadState _state;
    private readonly List<WorkerHandle> _workers = [];
    private readonly CancellationToken _parentToken;

    public WorkerPool(
        LoadTestOptions options,
        ILoadScenario scenario,
        MetricsCollector metrics,
        WorkloadState state,
        CancellationToken parentToken)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _parentToken = parentToken;
    }

    public int Count => _workers.Count;

    public void StartWorkers(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        for (int i = 0; i < count; i++)
        {
            CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_parentToken);
            ConnectionWorker worker = new(_options, _scenario, _metrics, _state);
            Task task = worker.RunAsync(cancellation.Token);
            _workers.Add(new WorkerHandle(task, cancellation));
        }
    }

    public async Task StopWorkersGraduallyAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        if (_workers.Count == 0)
        {
            return;
        }

        if (duration <= TimeSpan.Zero)
        {
            this.StopAll();
            return;
        }

        TimeSpan delay = TimeSpan.FromTicks(Math.Max(1, duration.Ticks / _workers.Count));
        for (int i = _workers.Count - 1; i >= 0; i--)
        {
            _workers[i].Stop();
            if (i > 0)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void StopAll()
    {
        for (int i = 0; i < _workers.Count; i++)
        {
            _workers[i].Stop();
        }
    }

    public async Task WaitAsync()
    {
        try
        {
            await Task.WhenAll(_workers.Select(static worker => worker.Task)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _workers.Count; i++)
        {
            _workers[i].Dispose();
        }
    }
}
