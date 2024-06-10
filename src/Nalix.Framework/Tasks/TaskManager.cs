// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Tasks;
using Nalix.Common.Tasks.Options;
using Nalix.Framework.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks.Options;

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

    private static readonly System.TimeSpan CleanupInterval = System.TimeSpan.FromSeconds(30);

    private readonly System.Threading.Timer _cleanupTimer;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, Gate> _groupGates;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IIdentifier, WorkerState> _workers;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, RecurringState> _recurring;

    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Ctors

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskManager"/> class.
    /// </summary>
    public TaskManager()
    {
        _workers = new();
        _recurring = new();
        _groupGates = new(System.StringComparer.Ordinal);

        _cleanupTimer = new System.Threading.Timer(static s =>
        {
            var self = (TaskManager)s!;
            self.CleanupWorkers();
        }, this, CleanupInterval, CleanupInterval);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[{nameof(TaskManager)}] init");
    }

    #endregion Ctors

    #region APIs

    /// <summary>
    /// Schedules a recurring background task.
    /// </summary>
    /// <param name="name">The unique name of the recurring task.</param>
    /// <param name="interval">The interval between executions.</param>
    /// <param name="work">The delegate representing the work to be performed.</param>
    /// <param name="options">Optional recurring options.</param>
    /// <returns>A handle to the scheduled recurring task.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the interval is less than or equal to zero.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if the work delegate is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if a recurring task with the same name already exists.</exception>
    public IRecurringHandle ScheduleRecurring(
        System.String name, System.TimeSpan interval,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        IRecurringOptions? options = null)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));

        System.ArgumentNullException.ThrowIfNull(work);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        System.ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, System.TimeSpan.Zero);

        options ??= new RecurringOptions();
        System.Threading.CancellationTokenSource cts = new();
        RecurringState st = new(name, interval, options, cts);

        if (!_recurring.TryAdd(name, st))
        {
            throw new System.InvalidOperationException($"[{nameof(TaskManager)}] duplicate recurring name: {name}");
        }

        st.Task = System.Threading.Tasks.Task.Run(() => RecurringLoopAsync(st, work), cts.Token);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TaskManager)}] start-recurring name={name} " +
                                       $"iv={interval.TotalMilliseconds:F0}ms " +
                                       $"nonReentrant={options.NonReentrant} tag={options.Tag ?? "-"}");
        return st;
    }

    /// <summary>
    /// Runs a single background job.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="work">The delegate representing the work to be performed.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if the work delegate is null.</exception>
    public async System.Threading.Tasks.ValueTask RunOnceAsync(
        System.String name,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));
        System.ArgumentNullException.ThrowIfNull(work);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(name);

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
                                    .Error($"[{nameof(TaskManager)}] run-once-error name={name} msg={ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Starts a worker task in the background.
    /// </summary>
    /// <param name="name">The name of the worker.</param>
    /// <param name="group">The group to which the worker belongs.</param>
    /// <param name="work">The delegate representing the work to be performed.</param>
    /// <param name="options">Optional worker options.</param>
    /// <returns>A handle to the started worker.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the name is null or whitespace.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if the work delegate is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the worker cannot be added.</exception>
    public IWorkerHandle StartWorker(
        System.String name, System.String group,
        System.Func<IWorkerContext, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        IWorkerOptions? options = null)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, nameof(TaskManager));
        System.ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (System.String.IsNullOrWhiteSpace(group))
        {
            group = "-";
        }

        System.ArgumentNullException.ThrowIfNull(work);

        options ??= new WorkerOptions();
        IIdentifier id = Identifier.NewId(options.IdType, options.MachineId);
        System.Threading.CancellationTokenSource cts = options.CancellationToken.CanBeCanceled
            ? System.Threading.CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken)
            : new System.Threading.CancellationTokenSource();

        WorkerState st = new(id, name, group, options, cts);

        if (!_workers.TryAdd(id, st))
        {
            throw new System.InvalidOperationException($"[{nameof(TaskManager)}] cannot add worker");
        }

        // Optional concurrency cap per-group
        Gate? gate = null;
        System.Exception? failure = null;

        if (options.GroupConcurrencyLimit is System.Int32 cap && cap > 0)
        {
            gate = _groupGates.GetOrAdd(group, _ => new Gate(new System.Threading.SemaphoreSlim(cap, cap), cap));
        }

        // run
        st.Task = System.Threading.Tasks.Task.Run(async () =>
        {
            System.Threading.CancellationToken ct = cts.Token;
            System.Boolean acquired = false;
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
                                                    .Warn($"[{nameof(TaskManager)}] " +
                                                          $"worker-reject name={name} group={group} reason=group-cap");

                            _ = _workers.TryRemove(id, out _);
                            try
                            {
                                cts.Dispose();
                            }
                            catch { }

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

                var ctx = new WorkerContext(st, this);
                await work(ctx, ct).ConfigureAwait(false);

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
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TaskManager)}] worker-error id={id} name={name} msg={ex.Message}");
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
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[{nameof(TaskManager)}] worker-callback-error id={id} msg={cbex.Message}");
                }

                if (gate is not null && acquired)
                {
                    try
                    {
                        _ = gate.SemaphoreSlim.Release();
                    }
                    catch { }
                }

                this.RetainOrRemove(st);
            }
        }, cts.Token);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(TaskManager)}] worker-start id={id} name={name} group={group} tag={options.Tag ?? "-"}");

        return st;
    }

    /// <summary>
    /// Cancels a recurring background task by its name.
    /// </summary>
    /// <param name="name">The name of the recurring task.</param>
    /// <returns><c>true</c> if the recurring task was found and cancelled; otherwise, <c>false</c>.</returns>
    [System.Diagnostics.Contracts.Pure]
    public System.Boolean CancelRecurring([System.Diagnostics.CodeAnalysis.MaybeNull] System.String? name)
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
                        try { st.Cts.Dispose(); } catch { }
                        try { st.Gate.Dispose(); } catch { }
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
                    st.Cts.Dispose();
                }
                catch { }
                try
                {
                    st.Gate.Dispose();
                }
                catch { }
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(TaskManager)}] cancel recurring name={name}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Cancels a worker task by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the worker.</param>
    /// <returns><c>true</c> if the worker was found and cancelled; otherwise, <c>false</c>.</returns>
    [System.Diagnostics.Contracts.Pure]
    public System.Boolean CancelWorker(IIdentifier id)
    {
        if (_workers.TryGetValue(id, out var st))
        {
            st.Cancel();

            System.Threading.Tasks.Task? t = st.Task;
            if (t?.IsCompleted == true)
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch { }
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(TaskManager)}] worker-cancel id={id} name={st.Name} group={st.Group}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Cancels all workers in a specific group.
    /// </summary>
    /// <param name="group">The group name.</param>
    /// <returns>The number of workers cancelled.</returns>
    [System.Diagnostics.Contracts.Pure]
    public System.Int32 CancelGroup(System.String group)
    {
        System.Int32 n = 0;
        foreach (var kv in _workers)
        {
            var st = kv.Value;
            if (System.String.Equals(st.Group, group, System.StringComparison.Ordinal))
            {
                if (!st.Cts.IsCancellationRequested)
                {
                    st.Cancel();
                    n++;
                }
            }
        }
        if (n > 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TaskManager)}] group-cancel group={group} count={n}");
        }

        return n;
    }

    /// <summary>
    /// Cancels all running workers.
    /// </summary>
    /// <returns>The number of workers cancelled.</returns>
    [System.Diagnostics.Contracts.Pure]
    public System.Int32 CancelAllWorkers()
    {
        System.Int32 n = 0;
        foreach (var kv in _workers)
        {
            if (!kv.Value.Cts.IsCancellationRequested)
            {
                kv.Value.Cancel(); n++;
            }
        }
        if (n > 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TaskManager)}] cancel-all-workers count={n}");
        }

        return n;
    }

    /// <summary>
    /// Tries to get a worker handle by identifier.
    /// </summary>
    /// <param name="id">The identifier of the worker.</param>
    /// <param name="handle">The handle of the worker, if found.</param>
    /// <returns><c>true</c> if found; otherwise, <c>false</c>.</returns>
    public System.Boolean TryGetWorker(
        IIdentifier id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IWorkerHandle? handle)
    {
        if (_workers.TryGetValue(id, out var st)) { handle = st; return true; }
        handle = null; return false;
    }

    /// <summary>
    /// Tries to get a recurring handle by name.
    /// </summary>
    /// <param name="name">The name of the recurring task.</param>
    /// <param name="handle">The handle of the recurring task, if found.</param>
    /// <returns><c>true</c> if found; otherwise, <c>false</c>.</returns>
    public System.Boolean TryGetRecurring(
        System.String name,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IRecurringHandle? handle)
    {
        if (_recurring.TryGetValue(name, out var st)) { handle = st; return true; }
        handle = null; return false;
    }

    /// <summary>
    /// Lists all scheduled recurring tasks.
    /// </summary>
    /// <returns>A read-only collection of recurring handles.</returns>
    public System.Collections.Generic.IReadOnlyCollection<IRecurringHandle> ListRecurring()
    {
        System.Collections.Generic.List<IRecurringHandle> list = new(_recurring.Count);
        foreach (var kv in _recurring)
        {
            list.Add(kv.Value);
        }

        return list;
    }

    /// <summary>
    /// Lists all worker handles.
    /// </summary>
    /// <param name="runningOnly">If <c>true</c>, only running workers are listed.</param>
    /// <param name="group">Optional group name to filter workers.</param>
    /// <returns>A read-only collection of worker handles.</returns>
    public System.Collections.Generic.IReadOnlyCollection<IWorkerHandle> ListWorkers(
        System.Boolean runningOnly = true, System.String? group = null)
    {
        System.Collections.Generic.List<IWorkerHandle> list = new(_workers.Count);
        foreach (var kv in _workers)
        {
            var st = kv.Value;
            if (runningOnly && !st.IsRunning)
            {
                continue;
            }

            if (group is not null && !System.String.Equals(st.Group, group, System.StringComparison.Ordinal))
            {
                continue;
            }

            list.Add(st);
        }
        return list;
    }

    #endregion APIs

    #region IReportable

    /// <summary>
    /// Generates a report summarizing all background tasks and workers.
    /// </summary>
    /// <returns>A formatted string containing report details.</returns>
    [System.Diagnostics.Contracts.Pure]
    public System.String GenerateReport()
    {
        var sb = new System.Text.StringBuilder(1024);
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TaskManager:");
        _ = sb.AppendLine($"Recurring: {_recurring.Count} | Workers: {_workers.Count} (running={CountRunningWorkers()})");
        _ = sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");

        // Recurring summary
        _ = sb.AppendLine("Recurring:");
        _ = sb.AppendLine("Naming                       | Runs     | Fails | Running | Last UTC             | Next UTC             | Interval | Tag");
        foreach (var kv in _recurring)
        {
            var s = kv.Value;
            System.String nm = PadName(kv.Key, 28);
            System.String runs = s.TotalRuns.ToString().PadLeft(8);
            System.String fails = s.ConsecutiveFailures.ToString().PadLeft(5);
            System.String run = s.IsRunning ? "yes" : " no";
            System.String last = s.LastRunUtc?.ToString("u") ?? "-";
            System.String next = s.NextRunUtc?.ToString("u") ?? "-";
            System.String iv = $"{s.Interval.TotalMilliseconds:F0}ms".PadLeft(8);
            System.String tag = s.Options.Tag ?? "-";
            _ = sb.AppendLine($"{nm} | {runs} | {fails} | {run.PadLeft(7)} | {last,-20} | {next,-20} | {iv} | {tag}");
        }
        _ = sb.AppendLine();

        // Workers summary by group
        _ = sb.AppendLine("Workers by Group:");
        _ = sb.AppendLine("Group                        | Running | Total | Concurrency");
        var perGroup = new System.Collections.Concurrent.ConcurrentDictionary<System.String, (System.Int32 running, System.Int32 total)>(System.StringComparer.Ordinal);
        foreach (var kv in _workers)
        {
            var g = kv.Value.Group;
            _ = perGroup.AddOrUpdate(g, _ => (kv.Value.IsRunning ? 1 : 0, 1),
                (_, t) => (t.running + (kv.Value.IsRunning ? 1 : 0), t.total + 1));
        }
        foreach (var gkv in perGroup)
        {
            System.String gname = PadName(gkv.Key, 28);
            if (_groupGates.TryGetValue(gkv.Key, out Gate? gate))
            {
                System.Int32 total = gate.Capacity;
                System.Int32 used = total - gate.SemaphoreSlim.CurrentCount;
                _ = sb.AppendLine($"{gname} | {gkv.Value.running,7} | {gkv.Value.total,5} | {used}/{total}");
            }
            else
            {
                _ = sb.AppendLine($"{gname} | {gkv.Value.running,7} | {gkv.Value.total,5} | -");
            }
        }
        _ = sb.AppendLine();

        // Top N long-running workers
        _ = sb.AppendLine("Top Running Workers (by age):");
        _ = sb.AppendLine("Id         | Naming                       | Group                        | Age     | Progress | LastBeat");
        var top = new System.Collections.Generic.List<WorkerState>(_workers.Values);
        top.Sort(static (a, b) => a.StartedUtc.CompareTo(b.StartedUtc)); // oldest first
        System.Int32 show = 0;
        foreach (var w in top)
        {
            if (!w.IsRunning)
            {
                continue;
            }

            _ = sb.AppendLine($"{w.Id} | {PadName(w.Name, 28)} | {PadName(w.Group, 28)} | {FormatAge(w.StartedUtc),7} | {w.Progress,8} | {w.LastHeartbeatUtc?.ToString("HH:mm:ss") ?? "-"}");
            if (++show >= 50)
            {
                break;
            }
        }

        _ = sb.AppendLine("--------------------------------------------------------------------------------------------------------");
        return sb.ToString();

        static System.String PadName(System.String s, System.Int32 width)
            => s.Length > width ? $"{System.MemoryExtensions.AsSpan(s, 0, width - 1)}…" : s.PadRight(width);

        static System.String FormatAge(System.DateTimeOffset start)
        {
            var ts = System.DateTimeOffset.UtcNow - start;
            return ts.TotalHours >= 1
                ? $"{(System.Int32)ts.TotalHours}h{ts.Minutes:D2}m"
                : ts.TotalMinutes >= 1 ? $"{(System.Int32)ts.TotalMinutes}m{ts.Seconds:D2}s" : $"{(System.Int32)ts.TotalSeconds}s";
        }
    }

    #endregion IReportable

    #region IDisposable

    /// <summary>
    /// Disposes the background task manager and cancels all running tasks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try { _cleanupTimer?.Dispose(); } catch { }

        foreach (var kv in _recurring)
        {
            var st = kv.Value;
            st.Cancel();

            var t = st.Task;
            if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                    {
                        try { st.Cts.Dispose(); } catch { }
                        try { st.Gate.Dispose(); } catch { }
                    },
                    System.Threading.CancellationToken.None,
                    System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                    System.Threading.Tasks.TaskScheduler.Default
                );
            }
            else
            {
                try { st.Cts.Dispose(); } catch { }
                try { st.Gate.Dispose(); } catch { }
            }
        }

        foreach (var kv in _workers)
        {
            var st = kv.Value;
            st.Cancel();

            var t = st.Task;
            if (t?.IsCompleted == true)
            {
                try { st.Cts.Dispose(); } catch { }
            }
            else if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                {
                    try { st.Cts.Dispose(); } catch { }
                }, System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);
            }
            else
            {
                try { st.Cts.Dispose(); } catch { }
            }
        }

        _recurring.Clear(); _workers.Clear();

        foreach (var g in _groupGates)
        {
            try { g.Value.SemaphoreSlim.Dispose(); } catch { }
        }

        _groupGates.Clear();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TaskManager)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}