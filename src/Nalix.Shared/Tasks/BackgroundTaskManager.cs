// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Shared.Tasks;

/// <summary>
/// Unified manager for background jobs and long-running workers:
/// - Recurring jobs: deadline-based ticks (Stopwatch), non-drift, non-reentrant (default), jitter, timeout, backoff.
/// - Workers: track thousands of long-running tasks (e.g., TCP accept/read loops), query counts by group,
///   cancellation by id/name/group, optional per-group concurrency cap, heartbeat and progress.
/// - Thread-safe, low allocation, server-grade reporting.
/// </summary>
public interface IBackgroundTaskManager : IDisposable, IReportable
{
    // ===== Recurring job API =====
    IRecurringHandle ScheduleRecurring(
        String name,
        TimeSpan interval,
        Func<CancellationToken, ValueTask> work,
        RecurringOptions? options = null);

    ValueTask RunOnce(String name, Func<CancellationToken, ValueTask> work, CancellationToken ct = default);

    Boolean CancelRecurring(String name);

    // ===== Worker API (long-running tasks) =====
    IWorkerHandle StartWorker(
        String name,
        String group,
        Func<IWorkerContext, CancellationToken, ValueTask> work,
        WorkerOptions? options = null);

    Boolean CancelWorker(Guid id);
    Int32 CancelGroup(String group);
    Int32 CancelAllWorkers();

    System.Collections.Generic.IReadOnlyCollection<IWorkerHandle> ListWorkers(Boolean runningOnly = true, String? group = null);
    Boolean TryGetWorker(Guid id, out IWorkerHandle? handle);

    // ===== Recurring listing =====
    System.Collections.Generic.IReadOnlyCollection<IRecurringHandle> ListRecurring();
    Boolean TryGetRecurring(String name, out IRecurringHandle? handle);

    // ===== Report/Diagnostics =====
    String Report();
    new String GenerateReport(); // alias
}

public sealed class RecurringOptions
{
    public Boolean NonReentrant { get; init; } = true;
    public TimeSpan? Jitter { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan? RunTimeout { get; init; }  // cancel a single run if exceeds
    public Int32 MaxFailuresBeforeBackoff { get; init; } = 1;
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromSeconds(15);
    public String? Tag { get; init; }
}

public sealed class WorkerOptions
{
    /// <summary>Retain finished workers for this duration (for diagnostics). Set null/TimeSpan.Zero to auto-remove.</summary>
    public TimeSpan? Retention { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Optional per-group concurrency cap. If set, executions in this group are gated.</summary>
    public Int32? MaxGroupConcurrency { get; init; }

    /// <summary>If true, acquire group slot immediately or cancel (no wait). Default: false (wait).</summary>
    public Boolean TryAcquireGroupSlotImmediately { get; init; } = false;

    /// <summary>Optional tag.</summary>
    public String? Tag { get; init; }
}

public interface IRecurringHandle : IDisposable
{
    String Name { get; }
    Boolean IsRunning { get; }
    Int64 TotalRuns { get; }
    Int32 ConsecutiveFailures { get; }
    DateTimeOffset? LastRunUtc { get; }
    DateTimeOffset? NextRunUtc { get; }
    TimeSpan Interval { get; }
    RecurringOptions Options { get; }
}

public interface IWorkerHandle : IDisposable
{
    Guid Id { get; }
    String Name { get; }
    String Group { get; }
    Boolean IsRunning { get; }
    Int64 TotalRuns { get; }              // for worker loops that internally iterate
    DateTimeOffset StartedUtc { get; }
    DateTimeOffset? LastHeartbeatUtc { get; }
    Int64 Progress { get; }               // user-defined units (bytes, messages…)
    String? LastNote { get; }
    WorkerOptions Options { get; }
}

public interface IWorkerContext
{
    Guid Id { get; }
    String Name { get; }
    String Group { get; }
    void Heartbeat();
    void AddProgress(Int64 delta, String? note = null);
    Boolean IsCancellationRequested { get; }
}

public sealed class BackgroundTaskManager : IBackgroundTaskManager
{
    private readonly ConcurrentDictionary<String, RecurringState> _recurring = new();
    private readonly ConcurrentDictionary<Guid, WorkerState> _workers = new();
    private readonly ConcurrentDictionary<String, SemaphoreSlim> _groupGates = new(StringComparer.Ordinal);
    private readonly ILogger? _log = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private volatile Boolean _disposed;

    // ===== Recurring API =====

    public IRecurringHandle ScheduleRecurring(
        String name,
        TimeSpan interval,
        Func<CancellationToken, ValueTask> work,
        RecurringOptions? options = null)
    {
        ThrowIfDisposed();
        if (String.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(nameof(name));
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (work is null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        options ??= new RecurringOptions();
        var cts = new CancellationTokenSource();
        var st = new RecurringState(name, interval, options, cts);

        if (!_recurring.TryAdd(name, st))
        {
            throw new InvalidOperationException($"[BackgroundTaskManager] duplicate recurring name: {name}");
        }

        st.Task = Task.Run(() => RecurringLoopAsync(st, work), cts.Token);

        _log?.Info($"[BackgroundTask] start recurring name={name} iv={interval.TotalMilliseconds:F0}ms nonReentrant={options.NonReentrant} tag={options.Tag ?? "-"}");
        return st;
    }

    public async ValueTask RunOnce(String name, Func<CancellationToken, ValueTask> work, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (String.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(nameof(name));
        }

        if (work is null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        try { await work(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[BackgroundTask] run-once-error name={name} msg={ex.Message}");
            throw;
        }
    }

    public Boolean CancelRecurring(String name)
    {
        if (_recurring.TryRemove(name, out var st))
        {
            st.Cancel();
            _log?.Warn($"[BackgroundTask] cancel recurring name={name}");
            return true;
        }
        return false;
    }

    public System.Collections.Generic.IReadOnlyCollection<IRecurringHandle> ListRecurring()
    {
        var list = new System.Collections.Generic.List<IRecurringHandle>(_recurring.Count);
        foreach (var kv in _recurring)
        {
            list.Add(kv.Value);
        }

        return list;
    }

    public Boolean TryGetRecurring(String name, out IRecurringHandle? handle)
    {
        if (_recurring.TryGetValue(name, out var st)) { handle = st; return true; }
        handle = null; return false;
    }

    // ===== Worker API =====

    public IWorkerHandle StartWorker(
        String name,
        String group,
        Func<IWorkerContext, CancellationToken, ValueTask> work,
        WorkerOptions? options = null)
    {
        ThrowIfDisposed();
        if (String.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(nameof(name));
        }

        if (String.IsNullOrWhiteSpace(group))
        {
            group = "-";
        }

        if (work is null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        options ??= new WorkerOptions();
        var id = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var st = new WorkerState(id, name, group, options, cts);

        if (!_workers.TryAdd(id, st))
        {
            throw new InvalidOperationException("[BackgroundTask] cannot add worker");
        }

        // Optional concurrency cap per-group
        SemaphoreSlim? gate = null;
        if (options.MaxGroupConcurrency is Int32 cap && cap > 0)
        {
            gate = _groupGates.GetOrAdd(group, _ => new SemaphoreSlim(cap, cap));
        }

        // run
        st.Task = Task.Run(async () =>
        {
            CancellationToken ct = cts.Token;
            try
            {
                if (gate is not null)
                {
                    if (options.TryAcquireGroupSlotImmediately)
                    {
                        if (!await gate.WaitAsync(0, ct).ConfigureAwait(false))
                        {
                            _log?.Warn($"[BackgroundTask] worker-reject name={name} group={group} reason=group-cap");
                            return;
                        }
                    }
                    else
                    {
                        await gate.WaitAsync(ct).ConfigureAwait(false);
                    }
                }

                st.MarkStart();

                var ctx = new WorkerContext(st, this);
                await work(ctx, ct).ConfigureAwait(false);

                st.MarkStop();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                st.MarkStop();
            }
            catch (Exception ex)
            {
                st.MarkError(ex);
                _log?.Error($"[BackgroundTask] worker-error id={id} name={name} msg={ex.Message}");
            }
            finally
            {
                if (gate is not null) { try { _ = gate.Release(); } catch { } }
                // auto-retention/cleanup
                RetainOrRemove(st);
            }
        }, cts.Token);

        _log?.Debug($"[BackgroundTask] worker-start id={id} name={name} group={group} tag={options.Tag ?? "-"}");
        return st;
    }

    public Boolean CancelWorker(Guid id)
    {
        if (_workers.TryGetValue(id, out var st))
        {
            st.Cancel();
            _log?.Warn($"[BackgroundTask] worker-cancel id={id} name={st.Name} group={st.Group}");
            return true;
        }
        return false;
    }

    public Int32 CancelGroup(String group)
    {
        Int32 n = 0;
        foreach (var kv in _workers)
        {
            var st = kv.Value;
            if (String.Equals(st.Group, group, StringComparison.Ordinal))
            {
                st.Cancel(); n++;
            }
        }
        if (n > 0)
        {
            _log?.Warn($"[BackgroundTask] group-cancel group={group} count={n}");
        }

        return n;
    }

    public Int32 CancelAllWorkers()
    {
        Int32 n = 0;
        foreach (var kv in _workers) { kv.Value.Cancel(); n++; }
        if (n > 0)
        {
            _log?.Warn($"[BackgroundTask] cancel-all-workers count={n}");
        }

        return n;
    }

    public System.Collections.Generic.IReadOnlyCollection<IWorkerHandle> ListWorkers(Boolean runningOnly = true, String? group = null)
    {
        var list = new System.Collections.Generic.List<IWorkerHandle>(_workers.Count);
        foreach (var kv in _workers)
        {
            var st = kv.Value;
            if (runningOnly && !st.IsRunning)
            {
                continue;
            }

            if (group is not null && !String.Equals(st.Group, group, StringComparison.Ordinal))
            {
                continue;
            }

            list.Add(st);
        }
        return list;
    }

    public Boolean TryGetWorker(Guid id, out IWorkerHandle? handle)
    {
        if (_workers.TryGetValue(id, out var st)) { handle = st; return true; }
        handle = null; return false;
    }

    // ===== Report =====

    public String GenerateReport()
    {
        var sb = new System.Text.StringBuilder(1024);
        _ = sb.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] BackgroundTaskManager:");
        _ = sb.AppendLine($"Recurring: {_recurring.Count} | Workers: {_workers.Count} (running={CountRunningWorkers()})");
        _ = sb.AppendLine("---------------------------------------------------------------------------------------");

        // Recurring summary
        _ = sb.AppendLine("Recurring:");
        _ = sb.AppendLine("Name                          | Runs     | Fails | Running | Last UTC              | Next UTC              | Interval | Tag");
        foreach (var kv in _recurring)
        {
            var s = kv.Value;
            String nm = PadName(kv.Key, 28);
            String runs = s.TotalRuns.ToString().PadLeft(8);
            String fails = s.ConsecutiveFailures.ToString().PadLeft(5);
            String run = s.IsRunning ? "yes" : " no";
            String last = s.LastRunUtc?.ToString("u") ?? "-";
            String next = s.NextRunUtc?.ToString("u") ?? "-";
            String iv = $"{s.Interval.TotalMilliseconds:F0}ms".PadLeft(8);
            String tag = s.Options.Tag ?? "-";
            _ = sb.AppendLine($"{nm} | {runs} | {fails} | {run.PadLeft(7)} | {last,-20} | {next,-20} | {iv} | {tag}");
        }
        _ = sb.AppendLine();

        // Workers summary by group
        _ = sb.AppendLine("Workers by Group:");
        _ = sb.AppendLine("Group                        | Running | Total  | Concurrency");
        var perGroup = new ConcurrentDictionary<String, (Int32 running, Int32 total)>(StringComparer.Ordinal);
        foreach (var kv in _workers)
        {
            var g = kv.Value.Group;
            _ = perGroup.AddOrUpdate(g, _ => (kv.Value.IsRunning ? 1 : 0, 1),
                (_, t) => (t.running + (kv.Value.IsRunning ? 1 : 0), t.total + 1));
        }
        foreach (var gkv in perGroup)
        {
            String gname = PadName(gkv.Key, 28);
            Int32 cap = _groupGates.TryGetValue(gkv.Key, out var sem) ? sem.CurrentCount + GetUsed(sem) : 0;
            String capStr = cap == 0 ? "-" : $"{cap}";
            _ = sb.AppendLine($"{gname} | {gkv.Value.running,7} | {gkv.Value.total,5} | {capStr}");
        }
        _ = sb.AppendLine();

        // Top N long-running workers
        _ = sb.AppendLine("Top Running Workers (by age):");
        _ = sb.AppendLine("Id                                 | Name                        | Group                       | Age     | Progress | LastBeat");
        var top = new System.Collections.Generic.List<WorkerState>(_workers.Values);
        top.Sort(static (a, b) => a.StartedUtc.CompareTo(b.StartedUtc)); // oldest first
        Int32 show = 0;
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

        _ = sb.AppendLine("---------------------------------------------------------------------------------------");
        return sb.ToString();

        static String PadName(String s, Int32 width) => s.Length > width ? String.Concat(s.AsSpan(0, width), "…") : s.PadRight(width);
        static Int32 GetUsed(SemaphoreSlim s) => Math.Max(0, s.CurrentCount - 0); // note: we cannot read max count; treat as unknown
        static String FormatAge(DateTimeOffset start)
        {
            var ts = DateTimeOffset.UtcNow - start;
            return ts.TotalHours >= 1
                ? $"{(Int32)ts.TotalHours}h{ts.Minutes:D2}m"
                : ts.TotalMinutes >= 1 ? $"{(Int32)ts.TotalMinutes}m{ts.Seconds:D2}s" : $"{(Int32)ts.TotalSeconds}s";
        }
    }

    private Int32 CountRunningWorkers()
    {
        Int32 n = 0; foreach (var kv in _workers)
        {
            if (kv.Value.IsRunning)
            {
                n++;
            }
        }

        return n;
    }

    // ===== Lifecycle =====

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var kv in _recurring)
        {
            kv.Value.Cancel();
        }

        foreach (var kv in _workers)
        {
            kv.Value.Cancel();
        }

        _recurring.Clear(); _workers.Clear();

        // gates: keep or dispose? generally keep process-lifetime; here we dispose.
        foreach (var g in _groupGates)
        {
            g.Value.Dispose();
        }

        _groupGates.Clear();

        _log?.Warn("[BackgroundTask] manager disposed");
        GC.SuppressFinalize(this);
    }

    // ===== Internals: Recurring loop (deadline-based, non-drift) =====

    private async Task RecurringLoopAsync(RecurringState s, Func<CancellationToken, ValueTask> work)
    {
        var ct = s.Cts.Token;

        // jitter
        if (s.Options.Jitter is { } j && j > TimeSpan.Zero)
        {
            try { await Task.Delay(Random.Shared.Next(0, (Int32)j.TotalMilliseconds), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }

        var freq = (Double)Stopwatch.Frequency;
        Int64 step = (Int64)(s.Interval.TotalSeconds * freq);
        if (step <= 0)
        {
            step = 1;
        }

        Int64 next = Stopwatch.GetTimestamp() + step;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                Int64 now = Stopwatch.GetTimestamp();
                Int64 delayTicks = next - now;
                if (delayTicks > 0)
                {
                    Int32 ms = (Int32)(delayTicks * 1000 / freq);
                    if (ms > 1)
                    {
                        await Task.Delay(ms, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
                else
                {
                    Int64 missed = (-delayTicks + step - 1) / step;
                    next += (missed + 1) * step;
                }

                if (s.Options.NonReentrant)
                {
                    if (!await s.Gate.WaitAsync(0, ct).ConfigureAwait(false))
                    {
                        next += step;
                        continue;
                    }
                }

                try
                {
                    s.MarkStart();

                    if (s.Options.RunTimeout is { } to && to > TimeSpan.Zero)
                    {
                        using var rcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        rcts.CancelAfter(to);
                        await work(rcts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await work(ct).ConfigureAwait(false);
                    }

                    s.MarkSuccess();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (OperationCanceledException oce)
                {
                    s.MarkFailure();
                    _log?.Warn($"[BackgroundTask] recurring-timeout name={s.Name} msg={oce.Message}");
                    await RecurringBackoffAsync(s, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    s.MarkFailure();
                    _log?.Error($"[BackgroundTask] recurring-error name={s.Name} msg={ex.Message}");
                    await RecurringBackoffAsync(s, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (s.Options.NonReentrant)
                    {
                        _ = s.Gate.Release();
                    }

                    next += step;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                s.MarkFailure();
                _log?.Error($"[BackgroundTask] recurring-loop-error name={s.Name} msg={ex.Message}");
                await RecurringBackoffAsync(s, ct).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask RecurringBackoffAsync(RecurringState s, CancellationToken ct)
    {
        Int32 n = Math.Max(1, s.Options.MaxFailuresBeforeBackoff);
        if (s.ConsecutiveFailures < n)
        {
            return;
        }

        Int32 pow = Math.Min(5, s.ConsecutiveFailures - n); // cap 32s
        Int32 ms = 1000 << pow;
        Int32 cap = (Int32)Math.Max(1, s.Options.MaxBackoff.TotalMilliseconds);
        if (ms > cap)
        {
            ms = cap;
        }

        try { await Task.Delay(ms, ct).ConfigureAwait(false); } catch (OperationCanceledException) { }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BackgroundTaskManager));
        }
    }

    private void RetainOrRemove(WorkerState st)
    {
        var keep = st.Options.Retention;
        if (keep is null || keep <= TimeSpan.Zero)
        {
            _ = _workers.TryRemove(st.Id, out _);
            return;
        }

        // schedule delayed removal
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(keep.Value).ConfigureAwait(false); } catch { }
            _ = _workers.TryRemove(st.Id, out _);
        });
    }

    public String Report() => throw new NotImplementedException();

    // ===== State classes =====

    private sealed class RecurringState : IRecurringHandle
    {
        public String Name { get; }
        public TimeSpan Interval { get; }
        public RecurringOptions Options { get; }
        public CancellationTokenSource Cts { get; }
        public Task? Task;
        public readonly SemaphoreSlim Gate = new(1, 1);

        public Int64 TotalRuns { get; private set; }
        public Int32 ConsecutiveFailures { get; private set; }
        public Boolean IsRunning { get; private set; }
        public DateTimeOffset? LastRunUtc { get; private set; }
        public DateTimeOffset? NextRunUtc => LastRunUtc?.Add(Interval);

        public RecurringState(String name, TimeSpan iv, RecurringOptions opt, CancellationTokenSource cts)
        { Name = name; Interval = iv; Options = opt; Cts = cts; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkStart() { IsRunning = true; LastRunUtc = DateTimeOffset.UtcNow; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkSuccess() { IsRunning = false; ConsecutiveFailures = 0; TotalRuns++; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkFailure() { IsRunning = false; ConsecutiveFailures++; TotalRuns++; }

        public void Cancel() => Cts.Cancel();

        // IRecurringHandle
        String IRecurringHandle.Name => Name;
        Boolean IRecurringHandle.IsRunning => IsRunning;
        Int64 IRecurringHandle.TotalRuns => TotalRuns;
        Int32 IRecurringHandle.ConsecutiveFailures => ConsecutiveFailures;
        DateTimeOffset? IRecurringHandle.LastRunUtc => LastRunUtc;
        DateTimeOffset? IRecurringHandle.NextRunUtc => NextRunUtc;
        TimeSpan IRecurringHandle.Interval => Interval;
        RecurringOptions IRecurringHandle.Options => Options;

        void IDisposable.Dispose() => Cancel();
    }

    private sealed class WorkerState(Guid id, String name, String group, WorkerOptions opt, CancellationTokenSource cts) : IWorkerHandle
    {
        public Guid Id { get; } = id;
        public String Name { get; } = name;
        public String Group { get; } = group;
        public WorkerOptions Options { get; } = opt;
        public CancellationTokenSource Cts { get; } = cts;
        public Task? Task;

        public Boolean IsRunning { get; private set; }
        public Int64 TotalRuns { get; private set; } // if loop reports iterations via context.AddProgress, you can also increment here
        public DateTimeOffset StartedUtc { get; private set; }
        public DateTimeOffset? LastHeartbeatUtc { get; private set; }
        public String? LastNote { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkStart() { IsRunning = true; StartedUtc = DateTimeOffset.UtcNow; LastHeartbeatUtc = StartedUtc; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkStop() => IsRunning = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkError(Exception _) => IsRunning = false; // could store last error detail if needed

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Beat() => LastHeartbeatUtc = DateTimeOffset.UtcNow;

        // Replace the Progress property backing field in WorkerState from:
        // public long Progress { get; private set; }
        // to:
        private Int64 _progress;
        public Int64 Progress => Interlocked.Read(ref _progress);

        public void Add(Int64 delta, String? note)
        {
            if (delta != 0)
            {
                _ = Interlocked.Add(ref _progress, delta);
            }

            if (note is not null)
            {
                LastNote = note;
            }

            LastHeartbeatUtc = DateTimeOffset.UtcNow;
        }

        public void Cancel() => Cts.Cancel();

        // IWorkerHandle
        Guid IWorkerHandle.Id => Id;
        String IWorkerHandle.Name => Name;
        String IWorkerHandle.Group => Group;
        Boolean IWorkerHandle.IsRunning => IsRunning;
        Int64 IWorkerHandle.TotalRuns => TotalRuns; // you can wire this if you want per-iteration counting
        DateTimeOffset IWorkerHandle.StartedUtc => StartedUtc;
        DateTimeOffset? IWorkerHandle.LastHeartbeatUtc => LastHeartbeatUtc;
        Int64 IWorkerHandle.Progress => Progress;
        String? IWorkerHandle.LastNote => LastNote;
        WorkerOptions IWorkerHandle.Options => Options;

        void IDisposable.Dispose() => Cancel();
    }

    private sealed class WorkerContext(BackgroundTaskManager.WorkerState st, BackgroundTaskManager owner) : IWorkerContext
    {
        private readonly WorkerState _st = st;
        private readonly BackgroundTaskManager _owner = owner;

        public Guid Id => _st.Id;
        public String Name => _st.Name;
        public String Group => _st.Group;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Heartbeat() => _st.Beat();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddProgress(Int64 delta, String? note = null) => _st.Add(delta, note);

        public Boolean IsCancellationRequested => _st.Cts.IsCancellationRequested;
    }
}
