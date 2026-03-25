// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Linq;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
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
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("TaskManager (Workers={_workers.Count}, Recurring={_recurring.Count})")]
public sealed partial class TaskManager : ITaskManager
{
    #region Fields

    private readonly TaskManagerOptions _options;
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly System.Threading.SemaphoreSlim _globalConcurrencyGate;
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
        _workerExecutionCount == 0 ? 0 : _workerExecutionTicks / (double)_workerExecutionCount / System.Diagnostics.Stopwatch.Frequency * 1000;

    /// <summary>
    /// Gets the average execution time for recurring tasks in milliseconds.
    /// </summary>
    public double AverageRecurringExecutionTime =>
        _recurringExecutionCount == 0 ? 0 : _recurringExecutionTicks / (double)_recurringExecutionCount / System.Diagnostics.Stopwatch.Frequency * 1000;

    /// <summary>
    /// Gets the total worker errors observed.
    /// </summary>
    public int WorkerErrorCount => System.Threading.Volatile.Read(ref _workerErrorCount);

    /// <summary>
    /// Gets the total recurring task errors observed.
    /// </summary>
    public int RecurringErrorCount => System.Threading.Volatile.Read(ref _recurringErrorCount);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskManager"/> class.
    /// </summary>
    /// <param name="options">Optional configuration options for the TaskManager.</param>
    public TaskManager(TaskManagerOptions? options = null)
    {
        _options = options ?? new TaskManagerOptions();
        _options.Validate();

        _workers = new();
        _recurring = new();
        _groupGates = new(System.StringComparer.Ordinal);
        _currentConcurrencyLimit = _options.MaxWorkers;
        _globalConcurrencyGate = new System.Threading.SemaphoreSlim(_currentConcurrencyLimit, _options.MaxWorkers);

        _cleanupTimer = new System.Threading.Timer(static s =>
        {
            TaskManager self = (TaskManager)s!;
            self.CLEANUP_WORKERS();
        }, this, _options.CleanupInterval, _options.CleanupInterval);

        if (_options.DynamicAdjustmentEnabled)
        {
            _ = ScheduleWorker(
                "task.monitor",
                "task",
                async (ctx, ct) => await MONITOR_CONCURRENCY_ASYNC(ctx, ct), // Pass CancellationToken
                new WorkerOptions
                {
                    RetainFor = System.TimeSpan.FromMinutes(10) // Cho phép giữ Monitor lâu hơn sau khi chạy xong
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
    /// <exception cref="System.ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if the work delegate is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the worker cannot be added.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public IWorkerHandle ScheduleWorker(
        [System.Diagnostics.CodeAnalysis.NotNull] string name,
        [System.Diagnostics.CodeAnalysis.NotNull] string group,
        [System.Diagnostics.CodeAnalysis.NotNull]
        System.Func<IWorkerContext, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        [System.Diagnostics.CodeAnalysis.MaybeNull] IWorkerOptions? options = null)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));

        if (string.IsNullOrWhiteSpace(group))
        {
            group = "-";
        }

        System.ArgumentNullException.ThrowIfNull(work);

        TimingScope scope = default;
        options ??= new WorkerOptions();

        // Acquire global concurrency slot
        _globalConcurrencyGate.Wait();

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        ISnowflake id = Snowflake.NewId(options.IdType, options.MachineId);
        System.Threading.CancellationTokenSource cts = options.CancellationToken.CanBeCanceled
            ? System.Threading.CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken)
            : new System.Threading.CancellationTokenSource();

        WorkerState st = new(id, name, group, options, cts);

        if (!_workers.TryAdd(id, st))
        {
            throw new System.InvalidOperationException($"[{nameof(TaskManager)}:{nameof(ScheduleWorker)}] cannot add worker");
        }

        // Optional concurrency cap per-group
        Gate? gate = null;
        System.Exception? failure = null;

        if (options.GroupConcurrencyLimit is int cap && cap > 0)
        {
            gate = _groupGates.GetOrAdd(group, _ => new Gate(new System.Threading.SemaphoreSlim(cap, cap), cap));
        }

        // run
        try
        {
            st.Task = System.Threading.Tasks.Task.Run(async () =>
            {
                bool acquired = false;
                System.Threading.CancellationToken ct = cts.Token;

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
                                catch (System.Exception ex)
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

                    if (options.ExecutionTimeout is { } to && to > System.TimeSpan.Zero)
                    {
                        using System.Threading.CancellationTokenSource wcts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
                        wcts.CancelAfter(to);
                        await work(new WorkerContext(st, this), wcts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await work(new WorkerContext(st, this), ct).ConfigureAwait(false);
                    }

                    st.MarkStop();
                }
                catch (System.OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    st.MarkStop();
                }
                catch (System.Exception ex)
                {
                    failure = ex;
                    st.MarkError(ex);
                    _ = System.Threading.Interlocked.Increment(ref _workerErrorCount);

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
                    catch (System.Exception cbex)
                    {
                        _ = System.Threading.Interlocked.Increment(ref _workerErrorCount);

                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[FW.{nameof(TaskManager)}] worker-callback-error id={id} msg={cbex.Message}");
                    }

                    if (gate is not null && acquired)
                    {
                        try
                        {
                            _ = gate.SemaphoreSlim.Release();
                        }
                        catch (System.Exception ex)
                        {
                            _ = System.Threading.Interlocked.Increment(ref _workerErrorCount);

                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}] gate-release-error id={id} msg={ex.Message}");
                        }
                    }

                    RETAIN_OR_REMOVE(st);
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
                _ = System.Threading.Interlocked.Increment(ref _workerExecutionCount);
                _ = System.Threading.Interlocked.Add(ref _workerExecutionTicks, (long)scope.GetElapsedMilliseconds());
            }
        }
    }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the interval is less than or equal to zero.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if the work delegate is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if a recurring task with the same name already exists.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public IRecurringHandle ScheduleRecurring(
        [System.Diagnostics.CodeAnalysis.StringSyntax("identifier")]
        [System.Diagnostics.CodeAnalysis.NotNull] string name,
        [System.Diagnostics.CodeAnalysis.NotNull] System.TimeSpan interval,
        [System.Diagnostics.CodeAnalysis.NotNull]
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        [System.Diagnostics.CodeAnalysis.MaybeNull] IRecurringOptions? options = null)
    {
        System.ArgumentNullException.ThrowIfNull(work);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));
        System.ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, System.TimeSpan.Zero);

        TimingScope scope = default;
        options ??= new RecurringOptions();
        System.Threading.CancellationTokenSource cts = new();
        RecurringState st = new(name, interval, options, cts);

        if (_options.IsEnableLatency)
        {
            scope = TimingScope.Start();
        }

        if (!_recurring.TryAdd(name, st))
        {
            _ = System.Threading.Interlocked.Increment(ref _recurringErrorCount);
            throw new System.InvalidOperationException($"[{nameof(TaskManager)}] duplicate recurring name: {name}");
        }

        try
        {
            st.Task = System.Threading.Tasks.Task.Run(() => RECURRING_LOOP_ASYNC(st, work), cts.Token);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[FW.{nameof(TaskManager)}:{nameof(ScheduleRecurring)}] start-recurring name={name} " +
                                           $"iv={interval.TotalMilliseconds:F0}ms nonReentrant={options.NonReentrant} tag={options.Tag ?? "-"}");
            return st;
        }
        finally
        {
            if (_options.IsEnableLatency)
            {
                _ = System.Threading.Interlocked.Add(ref _recurringExecutionTicks, (long)scope.GetElapsedMilliseconds());
                _ = System.Threading.Interlocked.Increment(ref _recurringExecutionCount);
            }
        }
    }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if the work delegate is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public async System.Threading.Tasks.ValueTask RunOnceAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] string name,
        [System.Diagnostics.CodeAnalysis.NotNull]
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(work);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(name);
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));

        try
        {
            await work(ct).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[FW.{nameof(TaskManager)}:{nameof(RunOnceAsync)}] run-once-error name={name} msg={ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public int CancelAllWorkers()
    {
        int n = 0;
        foreach (System.Collections.Generic.KeyValuePair<ISnowflake, WorkerState> kv in _workers)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public bool CancelWorker([System.Diagnostics.CodeAnalysis.NotNull] ISnowflake id)
    {
        if (_workers.TryGetValue(id, out WorkerState? st))
        {
            st.Cancel();

            System.Threading.Tasks.Task? t = st.Task;
            if (t?.IsCompleted == true)
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(CancelWorker)}] cts-dispose-error id={id} msg={ex.Message}");
                }
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(CancelWorker)}] worker-cancel id={id} name={st.Name} group={st.Group}");
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public int CancelGroup([System.Diagnostics.CodeAnalysis.NotNull] string group)
    {
        int n = 0;
        foreach (System.Collections.Generic.KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            if (string.Equals(st.Group, group, System.StringComparison.Ordinal) && !st.Cts.IsCancellationRequested)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public bool CancelRecurring([System.Diagnostics.CodeAnalysis.MaybeNull] string? name)
    {
        if (name is null)
        {
            return false;
        }

        if (_recurring.TryRemove(name, out RecurringState? st))
        {
            st.Cancel();

            System.Threading.Tasks.Task? t = st.Task;
            if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                {
                    try { st.CancellationTokenSource.Dispose(); }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cts-dispose-error name={name} msg={ex.Message}");
                    }
                    try { st.Gate.Dispose(); }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] gate-dispose-error name={name} msg={ex.Message}");
                    }
                },
                    System.Threading.CancellationToken.None,
                    System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                    System.Threading.Tasks.TaskScheduler.Default
                );
            }
            else
            {
                try
                {
                    st.CancellationTokenSource.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cts-dispose-error-sync name={name} msg={ex.Message}");
                }
                try
                {
                    st.Gate.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] gate-dispose-error-sync name={name} msg={ex.Message}");
                }
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(CancelRecurring)}] cancel recurring name={name}");
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Collections.Generic.IReadOnlyCollection<IWorkerHandle> GetWorkers(
        [System.Diagnostics.CodeAnalysis.NotNull] bool runningOnly = true,
        [System.Diagnostics.CodeAnalysis.MaybeNull] string? group = null)
    {
        System.Collections.Generic.List<IWorkerHandle> list = new(_workers.Count);
        foreach (System.Collections.Generic.KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            if (runningOnly && !st.IsRunning)
            {
                continue;
            }

            if (group is not null && !string.Equals(st.Group, group, System.StringComparison.Ordinal))
            {
                continue;
            }

            list.Add(st);
        }
        return list;
    }

    /// <inheritdoc/>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Collections.Generic.IReadOnlyCollection<IRecurringHandle> GetRecurring()
    {
        System.Collections.Generic.List<IRecurringHandle> list = new(_recurring.Count);
        foreach (System.Collections.Generic.KeyValuePair<string, RecurringState> kv in _recurring)
        {
            list.Add(kv.Value);
        }

        return list;
    }

    /// <inheritdoc/>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public bool TryGetWorker(
        ISnowflake id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IWorkerHandle? handle)
    {
        if (_workers.TryGetValue(id, out WorkerState? st)) { handle = st; return true; }
        handle = null;
        return false;
    }

    /// <inheritdoc/>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public bool TryGetRecurring(string name,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IRecurringHandle? handle)
    {
        if (_recurring.TryGetValue(name, out RecurringState? st)) { handle = st; return true; }
        handle = null;
        return false;
    }

    #endregion APIs

    #region IReportable

    /// <inheritdoc/>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        System.Text.StringBuilder sb = new(2048);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TaskManager:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring: {_recurring.Count} | Workers: {_workers.Count} (running={COUNT_RUNNING_WORKERS()})");
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
            System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
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
            System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
            proc.Refresh();

            _ = sb.AppendLine("Process Health:");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Threads                           : {proc.Threads.Count} (running: {proc.Threads.Cast<System.Diagnostics.ProcessThread>().Count(t => t.ThreadState == System.Diagnostics.ThreadState.Running)})");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Handles                           : {proc.HandleCount:N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"GC Collections                    : Gen0={System.GC.CollectionCount(0):N0} | Gen1={System.GC.CollectionCount(1):N0} | Gen2={System.GC.CollectionCount(2):N0}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Managed Heap                      : {System.GC.GetTotalMemory(false) / 1048576:N0} MB");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Uptime                            : {(System.DateTimeOffset.UtcNow - proc.StartTime.ToUniversalTime()).TotalDays:F1} days ({proc.StartTime:yyyy-MM-dd HH:mm:ss} UTC)");
            _ = sb.AppendLine("---------------------------------------------------------------------");
            _ = sb.AppendLine();
        }
        catch { }

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Monitoring Statistics:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Execution Count            : {_workerExecutionCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Worker Execution Time     : {AverageWorkerExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Worker Error Count                : {WorkerErrorCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Execution Count         : {_recurringExecutionCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Recurring Execution Time  : {AverageRecurringExecutionTime:F2} ms");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Recurring Error Count             : {RecurringErrorCount}");
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine();

        // Recurring summary
        _ = sb.AppendLine("Recurring:");
        _ = sb.AppendLine("---------------------------------------------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("Naming                       | Runs     | Fails | Running | Last UTC             | Next UTC             |  Interval | Tag        ");
        _ = sb.AppendLine("---------------------------------------------------------------------------------------------------------------------------------");
        foreach (System.Collections.Generic.KeyValuePair<string, RecurringState> kv in _recurring)
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
        System.Collections.Concurrent.ConcurrentDictionary<string, (int running, int total)> perGroup = new(System.StringComparer.Ordinal);
        foreach (System.Collections.Generic.KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            string g = kv.Value.Group;
            _ = perGroup.AddOrUpdate(g, _ => (kv.Value.IsRunning ? 1 : 0, 1),
                (_, t) => (t.running + (kv.Value.IsRunning ? 1 : 0), t.total + 1));
        }
        foreach (System.Collections.Generic.KeyValuePair<string, (int running, int total)> gkv in perGroup)
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
        System.Collections.Generic.List<WorkerState> top = [.. _workers.Values];
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
            => s.Length > width ? $"{System.MemoryExtensions.AsSpan(s, 0, width - 1)}…" : s.PadRight(width);

        static string FormatAge(System.DateTimeOffset start)
        {
            System.TimeSpan ts = System.DateTimeOffset.UtcNow - start;
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

    #endregion IReportable

    #region IDisposable

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] cleanup-timer-dispose-error msg={ex.Message}");
        }

        foreach (System.Collections.Generic.KeyValuePair<string, RecurringState> kv in _recurring)
        {
            RecurringState st = kv.Value;
            st.Cancel();

            System.Threading.Tasks.Task? t = st.Task;
            if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                    {
                        try { st.CancellationTokenSource.Dispose(); }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-cts-dispose-error name={st.Name} msg={ex.Message}");
                        }
                        try { st.Gate.Dispose(); }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-gate-dispose-error name={st.Name} msg={ex.Message}");
                        }
                    },
                    System.Threading.CancellationToken.None,
                    System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                    System.Threading.Tasks.TaskScheduler.Default
                );
            }
            else
            {
                try
                {
                    st.CancellationTokenSource.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-cts-dispose-error-sync name={st.Name} msg={ex.Message}");
                }
                try
                {
                    st.Gate.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] recurring-gate-dispose-error-sync name={st.Name} msg={ex.Message}");
                }
            }
        }

        foreach (System.Collections.Generic.KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            st.Cancel();

            System.Threading.Tasks.Task? t = st.Task;
            if (t?.IsCompleted == true)
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch (System.Exception ex)
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
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error-async id={st.Id} msg={ex.Message}");
                    }
                }, System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);
            }
            else
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] worker-cts-dispose-error-notask id={st.Id} msg={ex.Message}");
                }
            }
        }

        _recurring.Clear(); _workers.Clear();

        foreach (System.Collections.Generic.KeyValuePair<string, Gate> g in _groupGates)
        {
            try
            {
                g.Value.SemaphoreSlim.Dispose();
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] gate-dispose-error group={g.Key} msg={ex.Message}");
            }
        }

        _groupGates.Clear();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[FW.{nameof(TaskManager)}:{nameof(Dispose)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
