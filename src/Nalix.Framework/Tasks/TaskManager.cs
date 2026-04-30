// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Environment.Configuration;
using Nalix.Framework.Extensions;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;

namespace Nalix.Framework.Tasks;

/// <summary>
/// Coordinates background worker tasks and recurring jobs with shared concurrency,
/// cancellation, timing, and reporting support.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("TaskManager (Workers={_workers.Count}, Recurring={_recurring.Count})")]
public sealed partial class TaskManager : ITaskManager
{
    #region Fields

    private readonly Timer _cleanupTimer;
    private readonly Lock _pendingWorkersLock;
    private readonly Task _workerDispatcherTask;
    private readonly TaskManagerOptions _options;
    private readonly SemaphoreSlim _pendingWorkersSignal;
    private readonly SemaphoreSlim _globalConcurrencyGate;
    private readonly CancellationTokenSource _workerDispatcherCts;
    private readonly PriorityQueue<WorkerState, (int priorityKey, long sequence)> _pendingWorkers;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Gate> _groupGates;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, WorkerState> _workers;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RecurringState> _recurring;

    private int _workerErrorCount;
    private int _runningWorkerCount;
    private int _peakRunningWorkerCount;
    private int _recurringErrorCount;
    private long _workerWaitTicks;
    private long _workerExecutionTicks;
    private long _workerExecutionCount;
    private long _workerScheduleSequence;
    private long _recurringExecutionTicks;
    private long _recurringExecutionCount;
    private readonly long[] _workerLatencyBuckets = new long[11];

    private volatile bool _disposed;
    private volatile int _currentConcurrencyLimit;
    private int _concurrencyDeficiency;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a compact status line for consoles and diagnostics.
    /// </summary>
    /// <remarks>
    /// The title is intentionally short so it can be shown in window titles or
    /// one-line dashboards without formatting or truncation logic.
    /// </remarks>
    public string Title
    {
        get
        {
            int recurring = _recurring.Count;
            int totalWorkers = _workers.Count;
            int runningWorkers = Volatile.Read(ref _runningWorkerCount);
            return $"Workers: {runningWorkers} running / {totalWorkers} total | Recurring: {recurring}";
        }
    }

    /// <summary>
    /// Gets the average execution time for worker tasks in milliseconds.
    /// </summary>
    /// <remarks>
    /// The value is derived from the accumulated stopwatch ticks and the number
    /// of completed executions, so it reflects the long-term average rather than
    /// the most recent run.
    /// </remarks>
    public double AverageWorkerExecutionTime =>
        _workerExecutionCount == 0 ? 0 : _workerExecutionTicks / (double)_workerExecutionCount / Stopwatch.Frequency * 1000;

    /// <summary>
    /// Gets the average execution time for recurring tasks in milliseconds.
    /// </summary>
    /// <remarks>
    /// This uses the same tick-based accounting as worker timings so the report
    /// stays consistent across both task types.
    /// </remarks>
    public double AverageRecurringExecutionTime =>
        _recurringExecutionCount == 0 ? 0 : _recurringExecutionTicks / (double)_recurringExecutionCount / Stopwatch.Frequency * 1000;

    /// <summary>
    /// Gets the total worker errors observed.
    /// </summary>
    /// <remarks>
    /// The counter is read with <see langword="Volatile.Read"/> because it
    /// may be incremented from worker completion paths on different threads.
    /// </remarks>
    public int WorkerErrorCount => Volatile.Read(ref _workerErrorCount);

    /// <summary>
    /// Gets the total recurring task errors observed.
    /// </summary>
    /// <remarks>
    /// The counter is read with <see langword="Volatile.Read"/> because it
    /// may be updated concurrently by recurring execution paths.
    /// </remarks>
    public int RecurringErrorCount => Volatile.Read(ref _recurringErrorCount);

    /// <summary>
    /// Gets the peak number of concurrently running workers observed.
    /// </summary>
    public int PeakRunningWorkerCount => Volatile.Read(ref _peakRunningWorkerCount);

    /// <summary>
    /// Gets the average time workers spent in the queue before starting, in milliseconds.
    /// </summary>
    public double AverageWorkerWaitTime =>
        _workerExecutionCount == 0 ? 0 : (double)_workerWaitTicks / _workerExecutionCount / 10000.0;

    /// <summary>
    /// Gets the 95th percentile worker execution time in milliseconds (approximation).
    /// </summary>
    public double P95WorkerExecutionTime => this.GetWorkerPercentile(0.95);

    /// <summary>
    /// Gets the 99th percentile worker execution time in milliseconds (approximation).
    /// </summary>
    public double P99WorkerExecutionTime => this.GetWorkerPercentile(0.99);

    /// <summary>
    /// Calculates an approximate percentile for worker execution time based on histogram buckets.
    /// </summary>
    /// <param name="percentile">The percentile to calculate (e.g., 0.95 for P95).</param>
    /// <returns>The approximate latency in milliseconds.</returns>
    public double GetWorkerPercentile(double percentile)
    {
        long total = _workerExecutionCount;
        if (total == 0)
        {
            return 0;
        }

        long target = (long)(total * percentile);
        long accumulated = 0;

        double[] thresholds = [1.0, 5.0, 10.0, 25.0, 50.0, 100.0, 250.0, 500.0, 1000.0, 2500.0, 5000.0];

        for (int i = 0; i < _workerLatencyBuckets.Length; i++)
        {
            accumulated += Volatile.Read(ref _workerLatencyBuckets[i]);
            if (accumulated >= target)
            {
                return thresholds[i];
            }
        }

        return thresholds[^1];
    }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskManager"/> class.
    /// </summary>
    /// <remarks>
    /// The constructor wires up shared concurrency gates, periodic cleanup, and
    /// optional monitoring workers before any user-defined task is scheduled.
    /// </remarks>
    /// <param name="options">Optional configuration options for the TaskManager.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid scheduling limits.</exception>
    /// <exception cref="InvalidOperationException">Thrown when startup monitoring workers cannot be scheduled.</exception>
    public TaskManager(TaskManagerOptions? options = null)
    {
        _options = options ?? new TaskManagerOptions();
        _options.Validate();

        _workers = new();
        _recurring = new();
        _groupGates = new(StringComparer.Ordinal);
        _pendingWorkers = new();
        _pendingWorkersLock = new();
        _pendingWorkersSignal = new SemaphoreSlim(0, int.MaxValue);
        _workerDispatcherCts = new();
        _currentConcurrencyLimit = _options.MaxWorkers;
        _globalConcurrencyGate = new SemaphoreSlim(_currentConcurrencyLimit, _options.MaxWorkers);
        _workerDispatcherTask = this.WORKER_DISPATCH_LOOP_ASYNC(_workerDispatcherCts.Token);

        _cleanupTimer = new Timer(static s =>
        {
            TaskManager self = (TaskManager)s!;
            self.CLEANUP_WORKERS();
        }, this, _options.CleanupInterval, _options.CleanupInterval);

        if (_options.DynamicAdjustmentEnabled)
        {
            _ = this.ScheduleWorker(
                "task.monitor",
                "task",
                async (ctx, ct) => await this.MONITOR_CONCURRENCY_ASYNC(ctx, ct).ConfigureAwait(false),
                new WorkerOptions
                {
                    // Keep the monitor worker around longer so transient spikes can still be reported.
                    RetainFor = TimeSpan.FromMinutes(10)
                }
            );
        }

        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        if (logger != null && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"[FW.{nameof(TaskManager)}] init cleanup-iv={_options.CleanupInterval.TotalSeconds:F0}s concurrency={_currentConcurrencyLimit}");
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskManager"/> class.
    /// </summary>
    public TaskManager() : this(ConfigurationManager.Instance.Get<TaskManagerOptions>())
    {
    }

    #endregion Constructors

    #region APIs

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the work delegate is null.</exception>
    /// <exception cref="InternalErrorException">Thrown if the worker cannot be added.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the manager has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public IWorkerHandle ScheduleWorker(string name, string group, Func<IWorkerContext, CancellationToken, ValueTask> work, IWorkerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));

        if (string.IsNullOrWhiteSpace(group))
        {
            group = "-";
        }

        ArgumentNullException.ThrowIfNull(work);

        options ??= new WorkerOptions();

        ISnowflake id = Snowflake.NewId(options.IdType, options.MachineId);
#pragma warning disable CA2000 // Ownership is transferred to WorkerState and disposed during worker cleanup/manager teardown.
        CancellationTokenSource cts = options.CancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken)
            : new CancellationTokenSource();
#pragma warning restore CA2000

        WorkerState st = new(id, name, group, options, cts, work);

        if (!_workers.TryAdd(id, st))
        {
            cts.Dispose();
            throw new InternalErrorException($"[{nameof(TaskManager)}:{nameof(ScheduleWorker)}] cannot add worker");
        }

        bool startedFast = this.TRY_START_WORKER_FAST(st);
        if (!startedFast)
        {
            this.ENQUEUE_WORKER(st);
        }

        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"[FW.{nameof(TaskManager)}] worker-{(startedFast ? "start-fast" : "queued")} id={id} name={name} group={group} priority={options.Priority} tag={options.Tag ?? "-"}");
        }

        return st;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the interval is less than or equal to zero.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the work delegate is null.</exception>
    /// <exception cref="InternalErrorException">Thrown if a recurring task with the same name already exists.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the manager has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public IRecurringHandle ScheduleRecurring([StringSyntax("identifier")] string name, TimeSpan interval, Func<CancellationToken, ValueTask> work, IRecurringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        options ??= new RecurringOptions();
#pragma warning disable CA2000 // Ownership is transferred to RecurringState and disposed after its loop stops.
        CancellationTokenSource cts = new();
        RecurringState st = new(name, interval, options, cts);
#pragma warning restore CA2000

        if (!_recurring.TryAdd(name, st))
        {
            _ = Interlocked.Increment(ref _recurringErrorCount);
            throw new InternalErrorException($"[{nameof(TaskManager)}] duplicate recurring name: {name}");
        }

        st.Task = this.RECURRING_LOOP_ASYNC(st, work);

        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"[FW.{nameof(TaskManager)}:{nameof(ScheduleRecurring)}] start-recurring name={name} iv={interval.TotalMilliseconds:F0}ms nonReentrant={options.NonReentrant} tag={options.Tag ?? "-"}");
        }
        return st;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the work delegate is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the supplied <paramref name="ct"/> is cancelled while <paramref name="work"/> is running.</exception>
    /// <exception cref="Exception">Propagates any exception thrown by <paramref name="work"/> after it is logged.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the manager has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask RunOnceAsync(string name, Func<CancellationToken, ValueTask> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));

        try
        {
            await work(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError($"[FW.{nameof(TaskManager)}:{nameof(RunOnceAsync)}] run-once-error name={name} msg={ex.Message}");
            }
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when a matched worker's cancellation source has already been disposed.</exception>
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int CancelAllWorkers()
    {
        int n = 0;
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            if (!kv.Value.Cts.IsCancellationRequested)
            {
                kv.Value.Cancel(); n++;
            }
        }
        if (n > 0)
        {
            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"[FW.{nameof(TaskManager)}:{nameof(CancelAllWorkers)}] cancel-all-workers count={n}");
            }
        }

        return n;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the matched worker's cancellation source has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CancelWorker(ISnowflake id)
    {
        if (!_workers.TryGetValue(id, out WorkerState? st))
        {
            return;
        }

        st.Cancel();

        Task? t = st.Task;
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (t?.IsCompleted == true)
        {
            try
            {
                st.Cts.Dispose();
            }
            catch (ObjectDisposedException) { } // Ignore if already disposed
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (logger != null && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(CancelWorker)}] cts-dispose-error id={id} msg={ex.Message}");
                }
            }
        }

        if (logger != null && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"[FW.{nameof(TaskManager)}:{nameof(CancelWorker)}] worker-cancel id={id} name={st.Name} group={st.Group}");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when a matched worker's cancellation source has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int CancelGroup(string group)
    {
        int n = 0;
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            if (string.Equals(st.Group, group, StringComparison.Ordinal) && !st.Cts.IsCancellationRequested)
            {
                st.Cancel();
                n++;
            }
        }
        if (n > 0)
        {
            ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
            if (logger != null && logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"[FW.{nameof(TaskManager)}:{nameof(CancelGroup)}] group-cancel group={group} count={n}");
            }
        }

        return n;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown if the name is null or whitespace.</exception>"
    /// <exception cref="ObjectDisposedException">Thrown when the matched recurring task's cancellation source has already been disposed.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CancelRecurring(string? name)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

#pragma warning disable CA2000 // RecurringState is disposed asynchronously after its running task observes cancellation.
        if (!_recurring.TryRemove(name, out RecurringState? st))
#pragma warning restore CA2000
        {
            return;
        }

        st.Cancel();

        Task? t = st.Task;
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (t is not null)
        {
            _ = t.ContinueWith(_ =>
            {
                try { st.CancellationTokenSource.Dispose(); }
                catch (ObjectDisposedException) { }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (logger != null && logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cts-dispose-error name={name} msg={ex.Message}");
                    }
                }
                try { st.Gate.Dispose(); }
                catch (ObjectDisposedException) { }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (logger != null && logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] gate-dispose-error name={name} msg={ex.Message}");
                    }
                }
            },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }
        else
        {
            try
            {
                st.CancellationTokenSource.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (logger != null && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cts-dispose-error-sync name={name} msg={ex.Message}");
                }
            }
            try
            {
                st.Gate.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (logger != null && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] gate-dispose-error-sync name={name} msg={ex.Message}");
                }
            }
        }

        if (logger != null && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cancel recurring name={name}");
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public IReadOnlyCollection<IWorkerHandle> GetWorkers(bool runningOnly = true, string? group = null)
    {
        List<IWorkerHandle> list = new(_workers.Count);
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            if (runningOnly && !st.IsRunning)
            {
                continue;
            }

            if (group is not null && !string.Equals(st.Group, group, StringComparison.Ordinal))
            {
                continue;
            }

            list.Add(st);
        }
        return list;
    }

    /// <inheritdoc/>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public IReadOnlyCollection<IRecurringHandle> GetRecurring()
    {
        List<IRecurringHandle> list = new(_recurring.Count);
        foreach (KeyValuePair<string, RecurringState> kv in _recurring)
        {
            list.Add(kv.Value);
        }

        return list;
    }

    /// <inheritdoc/>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryGetWorker(
        ISnowflake id,
        [NotNullWhen(true)] out IWorkerHandle? handle)
    {
        if (_workers.TryGetValue(id, out WorkerState? st)) { handle = st; return true; }
        handle = null;
        return false;
    }

    /// <inheritdoc/>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryGetRecurring(string name,
        [NotNullWhen(true)] out IRecurringHandle? handle)
    {
        if (_recurring.TryGetValue(name, out RecurringState? st)) { handle = st; return true; }
        handle = null;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async Task WaitGroupAsync(string group, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(group));

        bool isPrefix = group.EndsWith('*');
        string matchGroup = isPrefix ? group[..^1] : group;

        List<Task> tasks = new();
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            bool match = isPrefix
                ? st.Group.StartsWith(matchGroup, StringComparison.Ordinal)
                : string.Equals(st.Group, matchGroup, StringComparison.Ordinal);

            if (match)
            {
                Task? t = st.Task;
                if (t is not null)
                {
                    tasks.Add(t);
                }
            }
        }

        if (tasks.Count == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // We only care about waiting, not the result of the tasks.
            // If they faulted, it's already logged by the worker loop.
            ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
            if (logger != null && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(WaitGroupAsync)}] group={group} some-tasks-failed msg={ex.Message}");
            }
        }
    }

    #endregion APIs

    #region IReportable

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new(2048);
        int runningWorkers = Volatile.Read(ref _runningWorkerCount);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TaskManager:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring: {_recurring.Count} | Workers: {_workers.Count} (running={runningWorkers})");
        _ = sb.AppendLine();

        // ========== CPU Monitoring Section ==========
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("CPU Monitoring:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Dynamic Adjustment Enabled        : {_options.DynamicAdjustmentEnabled}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Current Concurrency Limit         : {_currentConcurrencyLimit}/{_options.MaxWorkers}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"High CPU Threshold                : {_options.ThresholdHighCpu:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Low CPU Threshold                 : {_options.ThresholdLowCpu:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Observing Interval                : {_options.ObservingInterval.TotalSeconds:F1}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Warmup Duration                   : {_options.CpuWarmupDuration.TotalSeconds:F1}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Adjustment Streak Required        : {_options.AdjustmentStreakRequired}");
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine();

        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();

            long workingSetMB = proc.WorkingSet64 / (1024 * 1024);
            long privateMB = proc.PrivateMemorySize64 / (1024 * 1024);
            long virtualMB = proc.VirtualMemorySize64 / (1024 * 1024);

            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine("Memory Usage:");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Working Set                       : {workingSetMB,6:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Private Bytes                     : {privateMB,6:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Virtual Memory                    : {virtualMB,6:N0} MB");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(GenerateReport)}] memory-diagnostics-failed msg={ex.Message}");
            }
        }

        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();

            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int _);
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int _);

            int activeWorkerThreads = maxWorkerThreads - availableWorkerThreads;

            _ = sb.AppendLine("Process Health:");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Threads                           : {ThreadPool.ThreadCount} (running: {activeWorkerThreads})");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Completed Work Items              : {ThreadPool.CompletedWorkItemCount:N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Handles                           : {proc.HandleCount:N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GC Collections                    : Gen0={GC.CollectionCount(0):N0} | Gen1={GC.CollectionCount(1):N0} | Gen2={GC.CollectionCount(2):N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Managed Heap                      : {GC.GetTotalMemory(false) / 1048576:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Uptime                            : {(DateTimeOffset.UtcNow - proc.StartTime.ToUniversalTime()).TotalDays:F1} days ({proc.StartTime:yyyy-MM-dd HH:mm:ss} UTC)");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug($"[FW.{nameof(TaskManager)}:{nameof(GenerateReport)}] process-health-diagnostics-failed msg={ex.Message}");
            }
        }

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Monitoring Statistics:");
        double uptimeSec = (DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
        double workerTps = uptimeSec > 0 ? _workerExecutionCount / uptimeSec : 0;
        double recurringTps = uptimeSec > 0 ? _recurringExecutionCount / uptimeSec : 0;

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Execution Count            : {_workerExecutionCount} ({workerTps:F2} ops/s)");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Worker Execution Time     : {this.AverageWorkerExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"P95 Worker Execution Time         : <{this.P95WorkerExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"P99 Worker Execution Time         : <{this.P99WorkerExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Worker Wait Time          : {this.AverageWorkerWaitTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Peak Running Workers              : {this.PeakRunningWorkerCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Error Count                : {this.WorkerErrorCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Execution Count         : {_recurringExecutionCount} ({recurringTps:F2} ops/s)");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Recurring Execution Time  : {this.AverageRecurringExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Error Count             : {this.RecurringErrorCount}");
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Recurring summary
        List<RecurringState> recurringSnapshot = new(_recurring.Count);
        foreach (KeyValuePair<string, RecurringState> kv in _recurring)
        {
            recurringSnapshot.Add(kv.Value);
        }

        recurringSnapshot.Sort(static (a, b) => b.ConsecutiveFailures.CompareTo(a.ConsecutiveFailures));

        _ = sb.AppendLine("Recurring (Dashboard):");
        _ = sb.AppendLine("-----------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("NAMING                       | RUNS (T/F)    | RUN | SCHEDULE (L/N)          | INTERVAL  | TAG       ");
        _ = sb.AppendLine("-----------------------------+---------------+-----+-------------------------+-----------+-----------");
        foreach (RecurringState s in recurringSnapshot)
        {
            string nm = ReportExtensions.FormatTypeName(s.Name, 28);
            string runsFails = $"{s.TotalRuns.FormatCompact()} / {s.ConsecutiveFailures}";
            string run = s.IsRunning ? "yes" : " no";

            string last = s.LastRunUtc?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "--:--:--";
            string next = s.NextRunUtc?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "--:--:--";
            string schedule = $"{last} / {next}";

            string iv = s.Interval.FormatTimeSpan();
            string tag = s.Options.Tag ?? "-";
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{nm} | {runsFails,-13} | {run,3} | {schedule,-23} | {iv,9} | {tag}");
        }
        _ = sb.AppendLine("-----------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();
        _ = sb.AppendLine();

        // Workers summary by group
        _ = sb.AppendLine("Workers by Group:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("Group                        | Running | Total | Concurrency");
        _ = sb.AppendLine("-----------------------------+---------+-------+------------");
        Dictionary<string, (int running, int total)> perGroup = new(StringComparer.Ordinal);
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState worker = kv.Value;
            if (perGroup.TryGetValue(worker.Group, out (int running, int total) stats))
            {
                perGroup[worker.Group] = (stats.running + (worker.IsRunning ? 1 : 0), stats.total + 1);
            }
            else
            {
                perGroup[worker.Group] = (worker.IsRunning ? 1 : 0, 1);
            }
        }

        List<string> groupNames = new(perGroup.Count);
        foreach (KeyValuePair<string, (int running, int total)> gkv in perGroup)
        {
            groupNames.Add(gkv.Key);
        }

        groupNames.Sort(StringComparer.Ordinal);

        foreach (string groupName in groupNames)
        {
            string gname = PadName(groupName, 28);
            (int running, int total) = perGroup[groupName];
            if (_groupGates.TryGetValue(groupName, out Gate? gate))
            {
                int capacity = gate.Capacity;
                int used = capacity - gate.SemaphoreSlim.CurrentCount;
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{gname} | {running,7} | {total,5} | {used}/{capacity}");
            }
            else
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{gname} | {running,7} | {total,5} | -");
            }
        }
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine();

        // Top N long-running workers
        _ = sb.AppendLine("Top Running Workers (by age):");
        _ = sb.AppendLine("--------------------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("Id               | Naming                       | Group                        | Age     | Progress | LastBeat");
        _ = sb.AppendLine("-----------------+------------------------------+------------------------------+---------+----------+---------");
        List<WorkerState> top = new(_workers.Count);
        foreach (WorkerState worker in _workers.Values)
        {
            top.Add(worker);
        }

        top.Sort(static (a, b) => a.StartedUtc.CompareTo(b.StartedUtc)); // oldest first
        int show = 0;
        foreach (WorkerState w in top)
        {
            if (!w.IsRunning)
            {
                continue;
            }

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{w.Id} | {ReportExtensions.FormatTypeName(w.Name, 28)} | {ReportExtensions.FormatTypeName(w.Group, 28)} | {FormatAge(w.StartedUtc),7} | {w.Progress.FormatCompact(),8} | {w.LastHeartbeatUtc?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "-"}");
            if (++show >= 50)
            {
                break;
            }
        }

        _ = sb.AppendLine("-------------------------------------------------------------------------------------------------------------");
        return sb.ToString();

        static string PadName(string s, int width)
            => s.Length > width ? $"{MemoryExtensions.AsSpan(s, 0, width - 1)}…" : s.PadRight(width);

        static string FormatAge(DateTimeOffset start)
        {
            TimeSpan ts = DateTimeOffset.UtcNow - start;
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m";
            }
            else if (ts.TotalMinutes >= 1)
            {
                return $"{(int)ts.TotalMinutes}m{ts.Seconds:D2}s";
            }
            else
            {
                return $"{(int)ts.TotalSeconds}s";
            }
        }
    }

    /// <summary>
    /// Generates report data as key-value pairs describing the current state.
    /// </summary>
    /// <returns>A dictionary containing the report data.</returns>
    public IDictionary<string, object> GetReportData()
    {
        int runningWorkers = Volatile.Read(ref _runningWorkerCount);

        Dictionary<string, object> data = new(16, StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["RecurringCount"] = _recurring.Count,
            ["WorkersTotal"] = _workers.Count,
            ["WorkersRunning"] = runningWorkers,

            // CPU/Concurrency section
            ["DynamicAdjustmentEnabled"] = _options.DynamicAdjustmentEnabled,
            ["CurrentConcurrencyLimit"] = _currentConcurrencyLimit,
            ["MaxWorkers"] = _options.MaxWorkers,
            ["HighCpuThreshold"] = _options.ThresholdHighCpu,
            ["LowCpuThreshold"] = _options.ThresholdLowCpu,
            ["ObservingIntervalSeconds"] = _options.ObservingInterval.TotalSeconds,
            ["WarmupDurationSeconds"] = _options.CpuWarmupDuration.TotalSeconds,
            ["AdjustmentStreakRequired"] = _options.AdjustmentStreakRequired
        };

        // Memory usage (best effort)
        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();
            data["Memory"] = new Dictionary<string, long>(3, StringComparer.Ordinal)
            {
                ["WorkingSetMB"] = proc.WorkingSet64 / (1024 * 1024),
                ["PrivateMB"] = proc.PrivateMemorySize64 / (1024 * 1024),
                ["VirtualMB"] = proc.VirtualMemorySize64 / (1024 * 1024)
            };

            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int _);
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int _);

            int activeWorkerThreads = maxWorkerThreads - availableWorkerThreads;

            data["Process"] = new Dictionary<string, object>(8, StringComparer.Ordinal)
            {
                ["Threads"] = ThreadPool.ThreadCount,
                ["CompletedWorkItems"] = ThreadPool.CompletedWorkItemCount,
                ["ThreadsRunning"] = activeWorkerThreads,
                ["Handles"] = proc.HandleCount,
                ["GCGen0"] = GC.CollectionCount(0),
                ["GCGen1"] = GC.CollectionCount(1),
                ["GCGen2"] = GC.CollectionCount(2),
                ["ManagedHeapMB"] = GC.GetTotalMemory(false) / 1048576,
                ["UptimeDays"] = (DateTimeOffset.UtcNow - proc.StartTime.ToUniversalTime()).TotalDays,
                ["StartTimeUtc"] = proc.StartTime.ToUniversalTime()
            };
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Ignore diagnostics failure
        }

        // Stats
        data["WorkerExecutionCount"] = _workerExecutionCount;
        data["AverageWorkerExecutionTimeMs"] = this.AverageWorkerExecutionTime;
        data["P95WorkerExecutionTimeMs"] = this.P95WorkerExecutionTime;
        data["P99WorkerExecutionTimeMs"] = this.P99WorkerExecutionTime;
        data["AverageWorkerWaitTimeMs"] = this.AverageWorkerWaitTime;
        data["PeakRunningWorkerCount"] = this.PeakRunningWorkerCount;
        data["WorkerErrorCount"] = this.WorkerErrorCount;
        data["RecurringExecutionCount"] = _recurringExecutionCount;
        data["AverageRecurringExecutionTimeMs"] = this.AverageRecurringExecutionTime;
        data["RecurringErrorCount"] = this.RecurringErrorCount;

        // Recurring summary
        List<RecurringState> recurringSnapshot = new(_recurring.Count);
        foreach (RecurringState recurring in _recurring.Values)
        {
            recurringSnapshot.Add(recurring);
        }

        List<Dictionary<string, object>> recurringData = new(recurringSnapshot.Count);
        foreach (RecurringState s in recurringSnapshot)
        {
            recurringData.Add(new Dictionary<string, object>(8, StringComparer.Ordinal)
            {
                ["Name"] = s.Name,
                ["TotalRuns"] = s.TotalRuns,
                ["ConsecutiveFailures"] = s.ConsecutiveFailures,
                ["IsRunning"] = s.IsRunning,
                ["LastRunUtc"] = s.LastRunUtc ?? DateTimeOffset.MinValue,
                ["NextRunUtc"] = s.NextRunUtc ?? DateTimeOffset.MinValue,
                ["IntervalMs"] = s.Interval.TotalMilliseconds,
                ["Tag"] = s.Options.Tag ?? "N/A"
            });
        }

        data["Recurring"] = recurringData;

        // Top 5 Recurring by failures
        recurringSnapshot.Sort(static (a, b) => b.ConsecutiveFailures.CompareTo(a.ConsecutiveFailures));
        int topRecurringCount = recurringSnapshot.Count < 5 ? recurringSnapshot.Count : 5;
        List<Dictionary<string, object>> topRecurring = new(topRecurringCount);
        for (int i = 0; i < topRecurringCount; i++)
        {
            RecurringState r = recurringSnapshot[i];
            topRecurring.Add(new Dictionary<string, object>(4, StringComparer.Ordinal)
            {
                ["Name"] = r.Name,
                ["ConsecutiveFailures"] = r.ConsecutiveFailures,
                ["LastRunUtc"] = r.LastRunUtc ?? DateTimeOffset.MinValue,
                ["Tag"] = r.Options.Tag ?? "N/A"
            });
        }

        data["TopRecurringByFailures"] = topRecurring;

        // Workers by group
        Dictionary<string, (int running, int total)> groupCounts = new(StringComparer.Ordinal);
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            if (groupCounts.TryGetValue(st.Group, out (int running, int total) stats))
            {
                groupCounts[st.Group] = (stats.running + (st.IsRunning ? 1 : 0), stats.total + 1);
            }
            else
            {
                groupCounts[st.Group] = (st.IsRunning ? 1 : 0, 1);
            }
        }

        List<string> groupNames = new(groupCounts.Count);
        foreach (KeyValuePair<string, (int running, int total)> kv in groupCounts)
        {
            groupNames.Add(kv.Key);
        }

        groupNames.Sort(StringComparer.Ordinal);

        Dictionary<string, object> perGroup = new(groupCounts.Count, StringComparer.Ordinal);
        foreach (string groupName in groupNames)
        {
            (int running, int total) = groupCounts[groupName];
            string concurrency = _groupGates.TryGetValue(groupName, out Gate? gate)
                ? $"{gate.Capacity - gate.SemaphoreSlim.CurrentCount}/{gate.Capacity}"
                : "-";

            perGroup[groupName] = new Dictionary<string, object>(3, StringComparer.Ordinal)
            {
                ["Running"] = running,
                ["Total"] = total,
                ["Concurrency"] = concurrency
            };
        }

        data["WorkersByGroup"] = perGroup;

        // Top running workers (by age, max 50)
        List<WorkerState> runningSnapshot = new(_workers.Count);
        foreach (WorkerState worker in _workers.Values)
        {
            if (worker.IsRunning)
            {
                runningSnapshot.Add(worker);
            }
        }

        runningSnapshot.Sort(static (a, b) => a.StartedUtc.CompareTo(b.StartedUtc));
        int topRunningCount = runningSnapshot.Count < 50 ? runningSnapshot.Count : 50;
        List<Dictionary<string, object>> topRunning = new(topRunningCount);
        for (int i = 0; i < topRunningCount; i++)
        {
            WorkerState w = runningSnapshot[i];
            topRunning.Add(new Dictionary<string, object>(6, StringComparer.Ordinal)
            {
                ["Id"] = w.Id.ToString() ?? "N/A",
                ["Name"] = w.Name,
                ["Group"] = w.Group,
                ["StartedUtc"] = w.StartedUtc,
                ["Progress"] = w.Progress,
                ["LastHeartbeatUtc"] = w.LastHeartbeatUtc ?? DateTimeOffset.MinValue,
            });
        }

        data["TopRunningWorkers"] = topRunning;

        return data;
    }

    #endregion IReportable

    #region IDisposable

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        try
        {
            _workerDispatcherCts.Cancel();
            _ = _pendingWorkersSignal.Release();

            if (_workerDispatcherTask.IsCompleted)
            {
                if (_workerDispatcherTask.Exception?.GetBaseException() is Exception ex)
                {
                    if (logger != null && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-dispatcher-faulted msg={ex.Message}");
                    }
                }
            }
            else
            {
                _ = _workerDispatcherTask.ContinueWith(static (task) =>
                {
                    if (task.Exception?.GetBaseException() is Exception bgEx)
                    {
                        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } bgLogger && bgLogger.IsEnabled(LogLevel.Warning))
                        {
                            bgLogger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-dispatcher-faulted-after-dispose msg={bgEx.Message}");
                        }
                    }
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-dispatcher-stop-error msg={ex.Message}");
            }
        }

        try
        {
            _ = _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] cleanup-timer-stop-error msg={ex.Message}");
            }
        }

        try
        {
            _cleanupTimer?.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] cleanup-timer-dispose-error msg={ex.Message}");
            }
        }

        foreach (KeyValuePair<string, RecurringState> kv in _recurring)
        {
            RecurringState st = kv.Value;
            st.Cancel();

            Task? t = st.Task;
            if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                    {
                        try { st.CancellationTokenSource.Dispose(); }
                        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } innerLogger && innerLogger.IsEnabled(LogLevel.Warning))
                            {
                                innerLogger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-cts-dispose-error name={st.Name} msg={ex.Message}");
                            }
                        }
                        try { st.Gate.Dispose(); }
                        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } innerLogger && innerLogger.IsEnabled(LogLevel.Warning))
                            {
                                innerLogger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-gate-dispose-error name={st.Name} msg={ex.Message}");
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
            }
            else
            {
                try
                {
                    st.CancellationTokenSource.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (logger != null && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-cts-dispose-error-sync name={st.Name} msg={ex.Message}");
                    }
                }
                try
                {
                    st.Gate.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (logger != null && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-gate-dispose-error-sync name={st.Name} msg={ex.Message}");
                    }
                }
            }
        }

        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            st.Cancel();

            Task? t = st.Task;
            if (t?.IsCompleted == true)
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (logger != null && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error id={st.Id} msg={ex.Message}");
                    }
                }
            }
            else if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                {
                    try
                    {
                        st.Cts.Dispose();
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } innerLogger && innerLogger.IsEnabled(LogLevel.Warning))
                        {
                            innerLogger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error-async id={st.Id} msg={ex.Message}");
                        }
                    }
                }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            }
            else
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (logger != null && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error-notask id={st.Id} msg={ex.Message}");
                    }
                }
            }
        }

        _recurring.Clear(); _workers.Clear();

        foreach (KeyValuePair<string, Gate> g in _groupGates)
        {
            try
            {
                g.Value.SemaphoreSlim.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (logger != null && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] gate-dispose-error group={g.Key} msg={ex.Message}");
                }
            }
        }

        _groupGates.Clear();

        try
        {
            _pendingWorkersSignal.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] pending-signal-dispose-error msg={ex.Message}");
            }
        }

        try
        {
            _globalConcurrencyGate.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] global-gate-dispose-error msg={ex.Message}");
            }
        }

        try
        {
            _workerDispatcherCts.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (logger != null && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-dispatcher-cts-dispose-error msg={ex.Message}");
            }
        }

        if (logger != null && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] disposed");
        }

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
