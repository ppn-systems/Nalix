// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Time;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    #region Types

    private sealed record Gate(SemaphoreSlim SemaphoreSlim, int Capacity);

    /// <summary>
    /// Snapshot of CPU metrics for safe concurrent access.
    /// </summary>
    private sealed class CpuMetricsSnapshot
    {
        public double CurrentUsagePercent { get; set; }
        public long LastUpdateUtc { get; set; }
        public double ProcessorCount { get; set; }
    }

    #endregion Types

    #region Fields (CPU Monitoring)

    private long _lastCpuWallClockMs;
    private long _lastCpuProcessorTime;
    private readonly Stopwatch _cpuMeasureStopwatch = Stopwatch.StartNew();

    private int _lowCpuStreak;
    private int _highCpuStreak;

    private volatile bool _cpuWarmupDone;

    #endregion Fields (CPU Monitoring)

    #region Internal Cleanup

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void CLEANUP_WORKERS()
    {
        if (_disposed)
        {
            return;
        }

        // Cleanup only removes workers that already completed and stayed alive long
        // enough for their RetainFor window to expire.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            TimeSpan? keep = st.Options.RetainFor;

            if (!st.HasCompleted)
            {
                continue;
            }

            if (keep is null || keep <= TimeSpan.Zero)
            {
                continue;
            }

            if (st.IsRunning)
            {
                continue;
            }

            if (now - st.CompletedUtc < keep.Value)
            {
                continue;
            }

            if (_workers.TryRemove(st.Id, out _))
            {
                this.TRACE($"[FW.{nameof(TaskManager)}] cleanup-remove-ok id={st.Id}");
                try
                {
                    st.Cts.Dispose();
                }
                catch (Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}] cleanup-cts-dispose-error id={st.Id} msg={ex.Message}");
                }

                this.TRY_DISPOSE_GROUP_GATE_IF_UNUSED(st.Group);
            }
        }
    }

    #endregion Internal Cleanup

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void ENQUEUE_WORKER(WorkerState worker)
    {
        long sequence = Interlocked.Increment(ref _workerScheduleSequence);
        (int priorityKey, long sequence) priority = (-(int)worker.Options.Priority, sequence);

        lock (_pendingWorkersLock)
        {
            _pendingWorkers.Enqueue(worker, priority);
        }

        _ = _pendingWorkersSignal.Release();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private bool TRY_START_WORKER_FAST(WorkerState worker)
    {
        lock (_pendingWorkersLock)
        {
            if (_pendingWorkers.Count != 0)
            {
                return false;
            }

            if (!_globalConcurrencyGate.Wait(0))
            {
                return false;
            }
        }

        this.START_WORKER_EXECUTION(worker);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async Task WORKER_DISPATCH_LOOP_ASYNC(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _pendingWorkersSignal.WaitAsync(ct).ConfigureAwait(false);
                await _globalConcurrencyGate.WaitAsync(ct).ConfigureAwait(false);

                WorkerState? worker = null;

                try
                {
                    lock (_pendingWorkersLock)
                    {
                        if (_pendingWorkers.Count > 0)
                        {
                            worker = _pendingWorkers.Dequeue();
                        }
                    }

                    if (worker is null)
                    {
                        _ = _globalConcurrencyGate.Release();
                        continue;
                    }

                    this.START_WORKER_EXECUTION(worker);
                }
                catch (Exception ex)
                {
                    _ = _globalConcurrencyGate.Release();

                    if (worker is not null)
                    {
                        worker.MarkError(ex);
                        _ = Interlocked.Increment(ref _workerErrorCount);
                        this.RETAIN_OR_REMOVE(worker);
                    }

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}] worker-dispatch-error msg={ex.Message}");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}] worker-dispatch-loop-error msg={ex.Message}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void START_WORKER_EXECUTION(WorkerState st)
    {
        string name = st.Name;
        string group = st.Group;
        ISnowflake id = st.Id;
        IWorkerOptions options = st.Options;
        CancellationTokenSource cts = st.Cts;
        WorkerOptions? concreteOptions = options as WorkerOptions;

        Gate? gate = null;
        if (options.GroupConcurrencyLimit is int cap && cap > 0)
        {
            gate = _groupGates.GetOrAdd(group, static (_, capacity) => new Gate(new SemaphoreSlim(capacity, capacity), capacity), cap);
            if (gate.Capacity != cap)
            {
                throw new InvalidOperationException(
                    $"Worker group '{group}' already uses concurrency limit {gate.Capacity}, which conflicts with requested limit {cap}.");
            }
        }

        st.Task = Task.Run(async () =>
        {
            bool acquired = false;
            bool startedExecution = false;
            CancellationToken ct = cts.Token;
            bool completedSuccessfully = false;
            bool wasCancelled = false;
            Exception? failure = null;
            long executionStartTicks = 0;

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
                startedExecution = true;
                _ = Interlocked.Increment(ref _runningWorkerCount);
                executionStartTicks = _options.IsEnableLatency ? Stopwatch.GetTimestamp() : 0;
                WorkerContext ctx = new(st, this);

                if (options.ExecutionTimeout is { } to && to > TimeSpan.Zero)
                {
                    using CancellationTokenSource wcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    wcts.CancelAfter(to);
                    await st.Work(ctx, wcts.Token).ConfigureAwait(false);
                }
                else
                {
                    await st.Work(ctx, ct).ConfigureAwait(false);
                }

                completedSuccessfully = true;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                wasCancelled = true;
            }
            catch (Exception ex)
            {
                failure = ex;
                _ = Interlocked.Increment(ref _workerErrorCount);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[FW.{nameof(TaskManager)}] worker-error id={id} name={name} msg={ex.Message}");
            }
            finally
            {
                if (failure is not null)
                {
                    st.MarkError(failure);
                }
                else if (completedSuccessfully || wasCancelled)
                {
                    st.MarkStop();
                }

                try
                {
                    if (failure is null)
                    {
                        concreteOptions?.OnCompleted?.Invoke(st);
                    }
                    else
                    {
                        concreteOptions?.OnFailed?.Invoke(st, failure);
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

                if (startedExecution)
                {
                    _ = Interlocked.Decrement(ref _runningWorkerCount);

                    if (_options.IsEnableLatency && executionStartTicks != 0)
                    {
                        long elapsedTicks = Stopwatch.GetTimestamp() - executionStartTicks;
                        _ = Interlocked.Increment(ref _workerExecutionCount);
                        _ = Interlocked.Add(ref _workerExecutionTicks, elapsedTicks);
                    }
                }

                this.RETAIN_OR_REMOVE(st);
                _ = _globalConcurrencyGate.Release();
            }
        });

        this.TRACE($"[FW.{nameof(TaskManager)}] worker-start id={id} name={name} group={group} priority={options.Priority} tag={options.Tag ?? "-"}");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async Task RECURRING_LOOP_ASYNC(
        RecurringState s, Func<CancellationToken, ValueTask> work)
    {
        CancellationToken ct = s.CancellationTokenSource.Token;

        // Initial jitter spreads recurring jobs out so they do not all wake up together.
        if (s.Options.Jitter is { } j && j > TimeSpan.Zero)
        {
            try
            {
                int maxMs = (int)j.TotalMilliseconds;
                if (maxMs > 0)
                {
                    TimeSpan jitter = TimeSpan.FromMilliseconds(Csprng.GetInt32(0, maxMs));
                    await Task.Delay(jitter, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { return; }
        }

        // Work entirely in stopwatch ticks to avoid drift from repeated TimeSpan conversions.
        long step = s.IntervalTicks;
        long freq = Stopwatch.Frequency;
        long next = Stopwatch.GetTimestamp() + step;

        // Busy-wait is only used for tiny gaps where a full Task.Delay would overshoot too much.
        static void BusyWait(long untilTicks, CancellationToken ct)
        {
            SpinWait sw = new();
            while (Stopwatch.GetTimestamp() < untilTicks)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                sw.SpinOnce(sleep1Threshold: -1);
            }
        }

        // Above this threshold, a normal delay is cheaper and less CPU intensive than spinning.
        const double BusyWaitMaxSeconds = 0.0002; // 200 µs cap for busy spin

        while (!ct.IsCancellationRequested)
        {
            try
            {
                long now = Clock.MonoTicksNow();
                long delayTicks = next - now;

                if (delayTicks > 0)
                {
                    double delaySeconds = (double)delayTicks / freq;

                    if (delaySeconds <= BusyWaitMaxSeconds)
                    {
                        BusyWait(next, ct);
                    }
                    else
                    {
                        TimeSpan ts = TimeSpan.FromSeconds(delaySeconds);
                        await Task.Delay(ts, ct)
                                                         .ConfigureAwait(false);
                    }
                }
                else
                {
                    // If the job fell behind, skip the missed slots instead of replaying every one.
                    long missed = ((-delayTicks) + step - 1) / step;
                    next += (missed + 1) * step;
                }

                if (s.Options.NonReentrant)
                {
                    // A zero-timeout acquire keeps the scheduler from overlapping the same recurring job.
                    if (!await s.Gate.WaitAsync(0, ct).ConfigureAwait(false))
                    {
                        this.TRACE($"[FW.{nameof(TaskManager)}:Internal] gate-acquire-fail name={s.Name}");
                        next += step;
                        continue;
                    }
                }

                long executionStartTicks = 0;
                try
                {
                    s.MarkStart();
                    executionStartTicks = _options.IsEnableLatency ? Stopwatch.GetTimestamp() : 0;

                    if (s.Options.ExecutionTimeout is { } to && to > TimeSpan.Zero)
                    {
                        using CancellationTokenSource rcts =
                            CancellationTokenSource.CreateLinkedTokenSource(ct);

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
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-timeout name={s.Name} msg={oce.Message}");

                    await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    s.MarkFailure();
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-error name={s.Name} msg={ex.Message}");

                    await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (_options.IsEnableLatency && executionStartTicks != 0)
                    {
                        long elapsedTicks = Stopwatch.GetTimestamp() - executionStartTicks;
                        _ = Interlocked.Increment(ref _recurringExecutionCount);
                        _ = Interlocked.Add(ref _recurringExecutionTicks, elapsedTicks);
                    }

                    if (s.Options.NonReentrant)
                    {
                        try
                        {
                            _ = s.Gate.Release();
                        }
                        catch (Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}:Internal] gate-release-error name={s.Name} msg={ex.Message}");
                        }
                    }
                    next += step;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                s.MarkFailure();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-loop-error name={s.Name} msg={ex.Message}");

                await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
            }
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask RECURRING_BACKOFF_ASYNC(
        RecurringState s,
        CancellationToken ct)
    {
        // Backoff only starts after a configurable number of consecutive failures.
        int n = Math.Max(1, s.Options.FailuresBeforeBackoff);
        if (s.ConsecutiveFailures < n)
        {
            return;
        }

        // Cap the exponential growth so a broken recurring job does not disappear forever.
        int pow = Math.Min(5, s.ConsecutiveFailures - n); // cap at 2^5 = 32s
        int baseMs = 1000 << pow; // base delay: 1000ms * 2^pow
        int cap = (int)Math.Max(1, s.Options.BackoffCap.TotalMilliseconds);
        int maxDelay = Math.Min(baseMs, cap);

        // Full jitter avoids synchronized retries when many jobs fail at the same time.
        // Each retry gets a random delay inside the capped exponential window instead
        // of all workers retrying at the exact same moment.
        int delayMs = Csprng.GetInt32(0, maxDelay + 1);

        try
        {
            await Task.Delay(delayMs, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TRACE(string message) => InstanceManager.Instance.GetExistingInstance<ILogger>()?.Trace(message);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void RETAIN_OR_REMOVE(WorkerState st)
    {
        TimeSpan? keep = st.Options.RetainFor;
        // If retention is disabled, remove the worker as soon as it finishes.
        if (keep is null || keep <= TimeSpan.Zero)
        {
            _ = _workers.TryRemove(st.Id, out _);

            try
            {
                st.Cts.Dispose();
            }
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}] retain-cts-dispose-error id={st.Id} msg={ex.Message}");
            }

            this.TRY_DISPOSE_GROUP_GATE_IF_UNUSED(st.Group);

            return;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void TRY_DISPOSE_GROUP_GATE_IF_UNUSED(string group)
    {
        foreach (WorkerState other in _workers.Values)
        {
            if (string.Equals(other.Group, group, StringComparison.Ordinal))
            {
                return;
            }
        }

        if (_groupGates.TryRemove(group, out Gate? gate))
        {
            try
            {
                gate.SemaphoreSlim.Dispose();
                this.TRACE($"[FW.{nameof(TaskManager)}] group-gate-dispose-ok group={group}");
            }
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}] gate-dispose-error-retain group={group} msg={ex.Message}");
            }
        }
    }

    private async Task MONITOR_CONCURRENCY_ASYNC(IWorkerContext ctx, CancellationToken ct)
    {
        TaskManagerOptions options = _options;

        // Số lần liên tiếp vượt ngưỡng trước khi hành động (hysteresis)
        const int StreakRequired = 3;

        // Normalize threshold: config là % trên 1 core -> scale lên toàn bộ core
        double coreCount = System.Environment.ProcessorCount;
        double threshHigh = options.ThresholdHighCpu * coreCount;
        double threshLow = options.ThresholdLowCpu * coreCount;

        // Khởi tạo baseline CPU trước khi vòng lặp bắt đầu
        this.INITIALIZE_CPU_MEASUREMENT();

        while (!ct.IsCancellationRequested && options.DynamicAdjustmentEnabled)
        {
            try
            {
                double cpuUsage = this.MEASURE_CPU_USAGE_PERCENT();

                if (cpuUsage > threshHigh)
                {
                    this.TRACE($"[FW.{nameof(TaskManager)}:Internal] cpu-high usage={cpuUsage:F1}% threshold={threshHigh:F1}%");
                }

                // --- Hysteresis: tích streak, chỉ hành động khi đủ N lần liên tiếp ---
                if (cpuUsage > threshHigh && _currentConcurrencyLimit > 1)
                {
                    _lowCpuStreak = 0;
                    _highCpuStreak++;

                    if (_highCpuStreak >= StreakRequired)
                    {
                        _highCpuStreak = 0; // reset sau khi hành động
                        int newLimit = Math.Max(1, _currentConcurrencyLimit - 1);
                        this.ADJUST_CONCURRENCY(newLimit);
                    }
                }
                else if (cpuUsage < threshLow && _currentConcurrencyLimit < options.MaxWorkers)
                {
                    _highCpuStreak = 0;
                    _lowCpuStreak++;

                    if (_lowCpuStreak >= StreakRequired)
                    {
                        _lowCpuStreak = 0; // reset sau khi hành động
                        int newLimit = Math.Min(options.MaxWorkers, _currentConcurrencyLimit + 1);
                        this.ADJUST_CONCURRENCY(newLimit);
                    }
                }
                else
                {
                    // CPU trong vùng ổn định -> reset cả hai streak
                    _highCpuStreak = 0;
                    _lowCpuStreak = 0;
                }

                ctx.Beat();
                ctx.Advance(1);

                await Task.Delay(options.ObservingInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}:Internal] dynamic-adjustment-error ex={ex.Message}");
            }
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void INITIALIZE_CPU_MEASUREMENT()
    {
        Process proc = Process.GetCurrentProcess();
        Volatile.Write(ref _lastCpuProcessorTime, (long)proc.TotalProcessorTime.TotalMilliseconds);
        Volatile.Write(ref _lastCpuWallClockMs, _cpuMeasureStopwatch.ElapsedMilliseconds);
        _cpuWarmupDone = false;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private double MEASURE_CPU_USAGE_PERCENT()
    {
        long currentWallMs = _cpuMeasureStopwatch.ElapsedMilliseconds;

        // Warmup: trong 60 giây đầu, bỏ qua và KHÔNG cập nhật baseline
        // Điều này để lần đo sau warmup tính delta đúng kể từ lúc baseline được init
        if (!_cpuWarmupDone)
        {
            if (currentWallMs < 60_000)
            {
                return 0.0;
            }

            // Đánh dấu đã xong warmup, cập nhật baseline một lần ngay lúc này
            Process proc0 = Process.GetCurrentProcess();
            Volatile.Write(ref _lastCpuProcessorTime, (long)proc0.TotalProcessorTime.TotalMilliseconds);
            Volatile.Write(ref _lastCpuWallClockMs, currentWallMs);
            _cpuWarmupDone = true;
            return 0.0;
        }

        Process proc = Process.GetCurrentProcess();
        long currentCpuMs = (long)proc.TotalProcessorTime.TotalMilliseconds;

        long prevWallMs = Volatile.Read(ref _lastCpuWallClockMs);
        long prevCpuMs = Volatile.Read(ref _lastCpuProcessorTime);

        long wallDelta = currentWallMs - prevWallMs;
        long cpuDelta = currentCpuMs - prevCpuMs;

        // Cập nhật baseline cho lần đo tiếp theo
        Volatile.Write(ref _lastCpuWallClockMs, currentWallMs);
        Volatile.Write(ref _lastCpuProcessorTime, currentCpuMs);

        // Tránh chia cho 0 hoặc delta âm (clock skew, process refresh lag)
        if (wallDelta <= 0 || cpuDelta < 0)
        {
            return 0.0;
        }

        double processorCount = System.Environment.ProcessorCount;

        // cpuDelta / wallDelta = tỷ lệ sử dụng trên 1 core -> nhân processorCount -> % trên toàn bộ core
        double cpuUsagePercent = cpuDelta / (double)wallDelta * processorCount * 100.0;

        return Math.Min(cpuUsagePercent, processorCount * 100.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int COUNT_RUNNING_THREADS(Process proc)
    {
        int running = 0;
        foreach (ProcessThread thread in proc.Threads)
        {
            if (thread.ThreadState == System.Diagnostics.ThreadState.Running)
            {
                running++;
            }
        }

        return running;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ADJUST_CONCURRENCY(int newLimit)
    {
        // Safety: clamp to valid range
        newLimit = Math.Clamp(newLimit, 1, _options.MaxWorkers);

        int previousLimit = _currentConcurrencyLimit;

        // No change = skip
        if (previousLimit == newLimit)
        {
            return;
        }

        _currentConcurrencyLimit = newLimit;
        int delta = newLimit - previousLimit;

        try
        {
            if (delta > 0)
            {
                // Tăng capacity: release thêm slot vào semaphore
                _ = _globalConcurrencyGate.Release(delta);
            }
            else if (delta < 0)
            {
                // Giảm capacity: thu hồi slot bằng Wait(0) non-blocking
                // Nếu slot đang bị giữ (worker đang chạy), chấp nhận revert một phần
                // thay vì block — tránh deadlock
                int i;
                for (i = 0; i < -delta; i++)
                {
                    if (!_globalConcurrencyGate.Wait(0))
                    {
                        // Không còn slot rảnh -> revert về số đã thu hồi được thực tế
                        this.TRACE($"[FW.TaskManager.Internal] concurrency-partial-retreat from={previousLimit} to={previousLimit - i}");
                        _currentConcurrencyLimit = previousLimit - i;
                        break;
                    }
                }
            }

            this.TRACE($"[FW.TaskManager.Internal] concurrency-limit-adjusted=[{previousLimit}->{_currentConcurrencyLimit}]");
        }
        catch (Exception ex)
        {
            // Revert on error
            _currentConcurrencyLimit = previousLimit;

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[FW.TaskManager.Internal] failed-adjust-concurrency ex={ex.Message} from={previousLimit} to={newLimit}");
        }
    }
}
