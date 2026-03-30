// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Framework.Configuration;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Time;

namespace Nalix.Framework.Tasks;

/// <summary>
/// Manages background recurring tasks and worker tasks, providing scheduling, cancellation, and reporting functionalities.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("TaskManager (Workers={_workers.Count}, Recurring={_recurring.Count})")]
public sealed partial class TaskManager : ITaskManager
{
    #region Fields

    private readonly TaskManagerOptions _options;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _globalConcurrencyGate;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Gate> _groupGates;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ISnowflake, WorkerState> _workers;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RecurringState> _recurring;

    private long _workerExecutionTicks;
    private long _recurringExecutionTicks;
    private long _workerExecutionCount;
    private long _recurringExecutionCount;
    private int _workerErrorCount;
    private int _recurringErrorCount;

    private volatile bool _disposed;
    private volatile int _currentConcurrencyLimit;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a short console title summary containing running workers, total workers, and recurring tasks.
    /// </summary>
    public string Title
    {
        get
        {
            int recurring = _recurring.Count;
            int totalWorkers = _workers.Count;
            int runningWorkers = _workers.Values.Count(w => w.IsRunning);
            return $"Workers: {runningWorkers} running / {totalWorkers} total | Recurring: {recurring}";
        }
    }

    /// <summary>
    /// Gets the average execution time for worker tasks in milliseconds.
    /// </summary>
    public double AverageWorkerExecutionTime =>
        _workerExecutionCount == 0 ? 0 : _workerExecutionTicks / (double)_workerExecutionCount / Stopwatch.Frequency * 1000;

    /// <summary>
    /// Gets the average execution time for recurring tasks in milliseconds.
    /// </summary>
    public double AverageRecurringExecutionTime =>
        _recurringExecutionCount == 0 ? 0 : _recurringExecutionTicks / (double)_recurringExecutionCount / Stopwatch.Frequency * 1000;

    /// <summary>
    /// Gets the total worker errors observed.
    /// </summary>
    public int WorkerErrorCount => Volatile.Read(ref _workerErrorCount);

    /// <summary>
    /// Gets the total recurring task errors observed.
    /// </summary>
    public int RecurringErrorCount => Volatile.Read(ref _recurringErrorCount);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskManager"/> class.
    /// </summary>
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
        _currentConcurrencyLimit = _options.MaxWorkers;
        _globalConcurrencyGate = new SemaphoreSlim(_currentConcurrencyLimit, _options.MaxWorkers);

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
                async (ctx, ct) => await this.MONITOR_CONCURRENCY_ASYNC(ctx, ct).ConfigureAwait(false), // Pass CancellationToken
                new WorkerOptions
                {
                    RetainFor = TimeSpan.FromMinutes(10) // Cho phép giữ Monitor lâu hơn sau khi chạy xong
                }
            );
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[FW.{nameof(TaskManager)}] init cleanup-iv={_options.CleanupInterval.TotalSeconds:F0}s concurrency={_currentConcurrencyLimit}");
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

        TimingScope scope = default;
        options ??= new WorkerOptions();

        // Acquire global concurrency slot
        _globalConcurrencyGate.Wait();

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        ISnowflake id = Snowflake.NewId(options.IdType, options.MachineId);
        CancellationTokenSource cts = options.CancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken)
            : new CancellationTokenSource();

        WorkerState st = new(id, name, group, options, cts);

        if (!_workers.TryAdd(id, st))
        {
            throw new InternalErrorException($"[{nameof(TaskManager)}:{nameof(ScheduleWorker)}] cannot add worker");
        }

        // Optional concurrency cap per-group
        Gate? gate = null;
        Exception? failure = null;

        if (options.GroupConcurrencyLimit is int cap && cap > 0)
        {
            gate = _groupGates.GetOrAdd(group, _ => new Gate(new SemaphoreSlim(cap, cap), cap));
        }

        // run
        try
        {
            st.Task = Task.Run(async () =>
            {
                bool acquired = false;
                CancellationToken ct = cts.Token;

                try
                {
                    if (gate is not null)
                    {
                        if (options.TryAcquireSlotImmediately)
                        {
                            acquired = await gate.SemaphoreSlim.WaitAsync(0, ct)
                                                               .ConfigureAwait(false);
                            if (!acquired)
                            {
                                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                        .Warn($"[FW.{nameof(TaskManager)}] worker-reject name={name} group={group} reason=group-cap");

                                _ = _workers.TryRemove(id, out _);
                                try
                                {
                                    cts.Dispose();
                                }
                                catch (Exception ex)
                                {
                                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                            .Warn($"[FW.{nameof(TaskManager)}] cts-dispose-error-reject id={id} msg={ex.Message}");
                                }

                                return;
                            }
                        }
                        else
                        {
                            await gate.SemaphoreSlim.WaitAsync(ct)
                                                    .ConfigureAwait(false);
                            acquired = true;
                        }
                    }

                    st.MarkStart();
                    WorkerContext ctx = new(st, this);

                    if (options.ExecutionTimeout is { } to && to > TimeSpan.Zero)
                    {
                        using CancellationTokenSource wcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        wcts.CancelAfter(to);
                        await work(new WorkerContext(st, this), wcts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await work(new WorkerContext(st, this), ct).ConfigureAwait(false);
                    }

                    st.MarkStop();
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    st.MarkStop();
                }
                catch (Exception ex)
                {
                    failure = ex;
                    st.MarkError(ex);
                    _ = Interlocked.Increment(ref _workerErrorCount);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}] worker-error id={id} name={name} msg={ex.Message}");
                }
                finally
                {
                    try
                    {
                        if (failure is null)
                        {
                            (options as WorkerOptions)?.OnCompleted?.Invoke(st);
                        }
                        else
                        {
                            (options as WorkerOptions)?.OnFailed?.Invoke(st, failure);
                        }
                    }
                    catch (Exception cbex)
                    {
                        _ = Interlocked.Increment(ref _workerErrorCount);

                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[FW.{nameof(TaskManager)}] worker-callback-error id={id} msg={cbex.Message}");
                    }

                    if (gate is not null && acquired)
                    {
                        try
                        {
                            _ = gate.SemaphoreSlim.Release();
                        }
                        catch (Exception ex)
                        {
                            _ = Interlocked.Increment(ref _workerErrorCount);

                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}] gate-release-error id={id} msg={ex.Message}");
                        }
                    }

                    this.RETAIN_OR_REMOVE(st);
                    _ = _globalConcurrencyGate.Release();
                }
            }, cts.Token);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[FW.{nameof(TaskManager)}] worker-start id={id} name={name} group={group} tag={options.Tag ?? "-"}");

            return st;
        }
        finally
        {
            if (_options.IsEnableLatency)
            {
                _ = Interlocked.Increment(ref _workerExecutionCount);
                _ = Interlocked.Add(ref _workerExecutionTicks, (long)scope.GetElapsedMilliseconds());
            }
        }
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

        TimingScope scope = default;
        options ??= new RecurringOptions();
        CancellationTokenSource cts = new();
        RecurringState st = new(name, interval, options, cts);

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        if (!_recurring.TryAdd(name, st))
        {
            _ = Interlocked.Increment(ref _recurringErrorCount);
            throw new InternalErrorException($"[{nameof(TaskManager)}] duplicate recurring name: {name}");
        }

        try
        {
            st.Task = Task.Run(() => this.RECURRING_LOOP_ASYNC(st, work), cts.Token);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[FW.{nameof(TaskManager)}:{nameof(ScheduleRecurring)}] start-recurring name={name} " +
                                           $"iv={interval.TotalMilliseconds:F0}ms nonReentrant={options.NonReentrant} tag={options.Tag ?? "-"}");
            return st;
        }
        finally
        {
            if (_options.IsEnableLatency)
            {
                _ = Interlocked.Add(ref _recurringExecutionTicks, (long)scope.GetElapsedMilliseconds());
                _ = Interlocked.Increment(ref _recurringExecutionCount);
            }
        }
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
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[FW.{nameof(TaskManager)}:{nameof(RunOnceAsync)}] run-once-error name={name} msg={ex.Message}");
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
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[FW.{nameof(TaskManager)}:{nameof(CancelAllWorkers)}] cancel-all-workers count={n}");
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
        if (t?.IsCompleted == true)
        {
            try
            {
                st.Cts.Dispose();
            }
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[FW.{nameof(TaskManager)}:{nameof(CancelWorker)}] cts-dispose-error id={id} msg={ex.Message}");
            }
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[FW.{nameof(TaskManager)}:{nameof(CancelWorker)}] worker-cancel id={id} name={st.Name} group={st.Group}");
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
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[FW.{nameof(TaskManager)}:{nameof(CancelGroup)}] group-cancel group={group} count={n}");
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

        if (!_recurring.TryRemove(name, out RecurringState? st))
        {
            return;
        }

        st.Cancel();

        Task? t = st.Task;
        if (t is not null)
        {
            _ = t.ContinueWith(_ =>
            {
                try { st.CancellationTokenSource.Dispose(); }
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cts-dispose-error name={name} msg={ex.Message}");
                }
                try { st.Gate.Dispose(); }
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] gate-dispose-error name={name} msg={ex.Message}");
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
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cts-dispose-error-sync name={name} msg={ex.Message}");
            }
            try
            {
                st.Gate.Dispose();
            }
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] gate-dispose-error-sync name={name} msg={ex.Message}");
            }
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cancel recurring name={name}");
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

    #endregion APIs

    #region IReportable

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new(2048);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TaskManager:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring: {_recurring.Count} | Workers: {_workers.Count} (running={this.COUNT_RUNNING_WORKERS()})");
        _ = sb.AppendLine();

        // ========== CPU Monitoring Section ==========
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("CPU Monitoring:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Dynamic Adjustment Enabled       : {_options.DynamicAdjustmentEnabled}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Current Concurrency Limit         : {_currentConcurrencyLimit}/{_options.MaxWorkers}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"High CPU Threshold                : {_options.ThresholdHighCpu:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Low CPU Threshold                 : {_options.ThresholdLowCpu:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Observing Interval                : {_options.ObservingInterval.TotalSeconds:F1}s");
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine();

        // ========== Memory Monitoring usage ==========
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
        catch { /* best effort, ignore any exceptions from diagnostics */ }

        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();

            _ = sb.AppendLine("Process Health:");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Threads                           : {proc.Threads.Count} (running: {proc.Threads.Cast<ProcessThread>().Count(t => t.ThreadState == System.Diagnostics.ThreadState.Running)})");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Handles                           : {proc.HandleCount:N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GC Collections                    : Gen0={GC.CollectionCount(0):N0} | Gen1={GC.CollectionCount(1):N0} | Gen2={GC.CollectionCount(2):N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Managed Heap                      : {GC.GetTotalMemory(false) / 1048576:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Uptime                            : {(DateTimeOffset.UtcNow - proc.StartTime.ToUniversalTime()).TotalDays:F1} days ({proc.StartTime:yyyy-MM-dd HH:mm:ss} UTC)");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine();
        }
        catch { }

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Monitoring Statistics:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Execution Count            : {_workerExecutionCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Worker Execution Time     : {this.AverageWorkerExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Error Count                : {this.WorkerErrorCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Execution Count         : {_recurringExecutionCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Recurring Execution Time  : {this.AverageRecurringExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Error Count             : {this.RecurringErrorCount}");
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Recurring summary
        _ = sb.AppendLine("Recurring:");
        _ = sb.AppendLine("---------------------------------------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("Naming                       | Runs     | Fails | Running | Last UTC             | Next UTC             |  Interval | Tag        ");
        _ = sb.AppendLine("---------------------------------------------------------------------------------------------------------------------------------");
        foreach (KeyValuePair<string, RecurringState> kv in _recurring)
        {
            RecurringState s = kv.Value;
            string nm = PadName(kv.Key, 28);
            string runs = s.TotalRuns.ToString(CultureInfo.InvariantCulture).PadLeft(8);
            string fails = s.ConsecutiveFailures.ToString(CultureInfo.InvariantCulture).PadLeft(5);
            string run = s.IsRunning ? "yes" : " no";
            string last = s.LastRunUtc?.ToString("u") ?? "-";
            string next = s.NextRunUtc?.ToString("u") ?? "-";
            string iv = $"{s.Interval.TotalMilliseconds:F0}ms".PadLeft(9);
            string tag = s.Options.Tag ?? "-";
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{nm} | {runs} | {fails} | {run.PadLeft(7)} | {last,-20} | {next,-20} | {iv} | {tag}");
        }
        _ = sb.AppendLine("---------------------------------------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Top Recurring Tasks with Maximum Failures:");
        _ = sb.AppendLine("------------------------------------------------------------------------------");
        _ = sb.AppendLine("Name                         | Fails    | LastRun              | Tag          ");
        _ = sb.AppendLine("------------------------------------------------------------------------------");
        foreach (RecurringState r in _recurring.Values.OrderByDescending(r => r.ConsecutiveFailures).Take(5))
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{PadName(r.Name, 28)} | {r.ConsecutiveFailures,8} | {r.LastRunUtc?.ToString("u"),-20} | {r.Options.Tag ?? "-"}");
        }
        _ = sb.AppendLine("------------------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Workers summary by group
        _ = sb.AppendLine("Workers by Group:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine("Group                        | Running | Total | Concurrency");
        _ = sb.AppendLine("------------------------------------------------------------");
        System.Collections.Concurrent.ConcurrentDictionary<string, (int running, int total)> perGroup = new(StringComparer.Ordinal);
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            string g = kv.Value.Group;
            _ = perGroup.AddOrUpdate(g, _ => (kv.Value.IsRunning ? 1 : 0, 1),
                (_, t) => (t.running + (kv.Value.IsRunning ? 1 : 0), t.total + 1));
        }
        foreach (KeyValuePair<string, (int running, int total)> gkv in perGroup)
        {
            string gname = PadName(gkv.Key, 28);
            if (_groupGates.TryGetValue(gkv.Key, out Gate? gate))
            {
                int total = gate.Capacity;
                int used = total - gate.SemaphoreSlim.CurrentCount;
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{gname} | {gkv.Value.running,7} | {gkv.Value.total,5} | {used}/{total}");
            }
            else
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{gname} | {gkv.Value.running,7} | {gkv.Value.total,5} | -");
            }
        }
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine();

        // Top N long-running workers
        _ = sb.AppendLine("Top Running Workers (by age):");
        _ = sb.AppendLine("-------------------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("Id             | Naming                       | Group                        | Age     | Progress |  LastBeat");
        _ = sb.AppendLine("-------------------------------------------------------------------------------------------------------------");
        List<WorkerState> top = [.. _workers.Values];
        top.Sort(static (a, b) => a.StartedUtc.CompareTo(b.StartedUtc)); // oldest first
        int show = 0;
        foreach (WorkerState w in top)
        {
            if (!w.IsRunning)
            {
                continue;
            }

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{w.Id} | {PadName(w.Name, 28)} | {PadName(w.Group, 28)} | {FormatAge(w.StartedUtc),7} | {w.Progress,8} |  {w.LastHeartbeatUtc?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "-"}");
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
    public IDictionary<string, object> GenerateReportData()
    {
        Dictionary<string, object> data = new(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["RecurringCount"] = _recurring.Count,
            ["WorkersTotal"] = _workers.Count,
            ["WorkersRunning"] = _workers.Values.Count(w => w.IsRunning),

            // CPU/Concurrency section
            ["DynamicAdjustmentEnabled"] = _options.DynamicAdjustmentEnabled,
            ["CurrentConcurrencyLimit"] = _currentConcurrencyLimit,
            ["MaxWorkers"] = _options.MaxWorkers,
            ["HighCpuThreshold"] = _options.ThresholdHighCpu,
            ["LowCpuThreshold"] = _options.ThresholdLowCpu,
            ["ObservingIntervalSeconds"] = _options.ObservingInterval.TotalSeconds
        };

        // Memory usage (best effort)
        try
        {
            Process proc = Process.GetCurrentProcess();
            proc.Refresh();
            data["Memory"] = new Dictionary<string, long>
            {
                ["WorkingSetMB"] = proc.WorkingSet64 / (1024 * 1024),
                ["PrivateMB"] = proc.PrivateMemorySize64 / (1024 * 1024),
                ["VirtualMB"] = proc.VirtualMemorySize64 / (1024 * 1024)
            };

            data["Process"] = new Dictionary<string, object>
            {
                ["Threads"] = proc.Threads.Count,
                ["ThreadsRunning"] = proc.Threads.Cast<ProcessThread>().Count(t => t.ThreadState == System.Diagnostics.ThreadState.Running),
                ["Handles"] = proc.HandleCount,
                ["GCGen0"] = GC.CollectionCount(0),
                ["GCGen1"] = GC.CollectionCount(1),
                ["GCGen2"] = GC.CollectionCount(2),
                ["ManagedHeapMB"] = GC.GetTotalMemory(false) / 1048576,
                ["UptimeDays"] = (DateTimeOffset.UtcNow - proc.StartTime.ToUniversalTime()).TotalDays,
                ["StartTimeUtc"] = proc.StartTime.ToUniversalTime()
            };
        }
        catch
        {
            // Ignore diagnostics failure
        }

        // Stats
        data["WorkerExecutionCount"] = _workerExecutionCount;
        data["AverageWorkerExecutionTimeMs"] = this.AverageWorkerExecutionTime;
        data["WorkerErrorCount"] = this.WorkerErrorCount;
        data["RecurringExecutionCount"] = _recurringExecutionCount;
        data["AverageRecurringExecutionTimeMs"] = this.AverageRecurringExecutionTime;
        data["RecurringErrorCount"] = this.RecurringErrorCount;

        // Recurring summary
        data["Recurring"] = _recurring.Values.Select(s => new Dictionary<string, object>
        {
            ["Name"] = s.Name,
            ["TotalRuns"] = s.TotalRuns,
            ["ConsecutiveFailures"] = s.ConsecutiveFailures,
            ["IsRunning"] = s.IsRunning,
            ["LastRunUtc"] = s.LastRunUtc ?? DateTimeOffset.MinValue,
            ["NextRunUtc"] = s.NextRunUtc ?? DateTimeOffset.MinValue,
            ["IntervalMs"] = s.Interval.TotalMilliseconds,
            ["Tag"] = s.Options.Tag ?? "N/A"
        }).ToList();

        // Top 5 Recurring by failures
        data["TopRecurringByFailures"] = _recurring.Values
            .OrderByDescending(r => r.ConsecutiveFailures)
            .Take(5)
            .Select(r => new Dictionary<string, object>
            {
                ["Name"] = r.Name,
                ["ConsecutiveFailures"] = r.ConsecutiveFailures,
                ["LastRunUtc"] = r.LastRunUtc ?? DateTimeOffset.MinValue,
                ["Tag"] = r.Options.Tag ?? "N/A"
            }).ToList();

        // Workers by group
        Dictionary<string, object> perGroup = new(StringComparer.Ordinal);
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            if (!perGroup.TryGetValue(st.Group, out object? v))
            {
                perGroup[st.Group] = new { Running = 0, Total = 0, Concurrency = "" };
                v = perGroup[st.Group];
            }
            dynamic rec = v;
            perGroup[st.Group] = new
            {
                Running = rec.Running + (st.IsRunning ? 1 : 0),
                Total = rec.Total + 1,
                Concurrency = _groupGates.TryGetValue(st.Group, out Gate? gate)
                    ? $"{gate.Capacity - gate.SemaphoreSlim.CurrentCount}/{gate.Capacity}"
                    : "-"
            };
        }
        data["WorkersByGroup"] = perGroup;

        // Top running workers (by age, max 50)
        data["TopRunningWorkers"] = _workers.Values
            .Where(w => w.IsRunning)
            .OrderBy(w => w.StartedUtc)
            .Take(50)
            .Select(w => new Dictionary<string, object>
            {
                ["Id"] = w.Id.ToString() ?? "N/A",
                ["Name"] = w.Name,
                ["Group"] = w.Group,
                ["StartedUtc"] = w.StartedUtc,
                ["Progress"] = w.Progress,
                ["LastHeartbeatUtc"] = w.LastHeartbeatUtc ?? DateTimeOffset.MinValue,
            }).ToList();

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

        try
        {
            _cleanupTimer?.Dispose();
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] cleanup-timer-dispose-error msg={ex.Message}");
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
                        catch (Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-cts-dispose-error name={st.Name} msg={ex.Message}");
                        }
                        try { st.Gate.Dispose(); }
                        catch (Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-gate-dispose-error name={st.Name} msg={ex.Message}");
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
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-cts-dispose-error-sync name={st.Name} msg={ex.Message}");
                }
                try
                {
                    st.Gate.Dispose();
                }
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-gate-dispose-error-sync name={st.Name} msg={ex.Message}");
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
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error id={st.Id} msg={ex.Message}");
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
                    catch (Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error-async id={st.Id} msg={ex.Message}");
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
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error-notask id={st.Id} msg={ex.Message}");
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
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] gate-dispose-error group={g.Key} msg={ex.Message}");
            }
        }

        _groupGates.Clear();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] disposed");

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
