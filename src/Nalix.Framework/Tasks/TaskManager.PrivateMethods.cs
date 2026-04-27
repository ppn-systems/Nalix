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
using Nalix.Environment.Random;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
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
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[FW.{nameof(TaskManager)}] cleanup-cts-dispose-error id={st.Id} msg={ex.Message}");
                    }
                }

                this.TRY_DISPOSE_GROUP_GATE_IF_UNUSED(st.Group);
            }
        }
    }

    #endregion Internal Cleanup

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void ENQUEUE_WORKER(WorkerState worker)
    {
        worker.MarkScheduled();
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

            worker.MarkScheduled(); // Mark even for fast-start so wait time is 0
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
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    _ = _globalConcurrencyGate.Release();

                    if (worker is not null)
                    {
                        worker.MarkError(ex);
                        _ = Interlocked.Increment(ref _workerErrorCount);
                        this.RETAIN_OR_REMOVE(worker);
                    }

                    if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
                    {
                        logger.LogError($"[FW.{nameof(TaskManager)}] worker-dispatch-error msg={ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"[FW.{nameof(TaskManager)}] worker-dispatch-loop-error msg={ex.Message}");
                }
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

        if (options.OSPriority is ThreadPriority osPriority)
        {
            TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            st.Task = tcs.Task;

            Thread thread = new(() =>
            {
                try
                {
                    Thread.CurrentThread.Priority = osPriority;
                    Task execution = this.EXECUTE_WORKER_ASYNC(st, gate, cts);
                    if (execution.IsCompletedSuccessfully)
                    {
                        _ = tcs.TrySetResult();
                        return;
                    }

                    _ = execution.ContinueWith(static (task, state) =>
                    {
                        if (state is not TaskCompletionSource source)
                        {
                            return;
                        }

                        if (task.IsCanceled)
                        {
                            _ = source.TrySetCanceled();
                            return;
                        }

                        if (task.Exception?.GetBaseException() is Exception ex)
                        {
                            _ = source.TrySetException(ex);
                            return;
                        }

                        _ = source.TrySetResult();
                    }, tcs, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    _ = tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = name
            };
            thread.Start();

            this.TRACE($"[FW.{nameof(TaskManager)}] worker-start-dedicated id={id} name={name} group={group} ospriority={osPriority} tag={options.Tag ?? "-"}");
        }
        else
        {
            st.Task = this.EXECUTE_WORKER_ASYNC(st, gate, cts);
            this.TRACE($"[FW.{nameof(TaskManager)}] worker-start id={id} name={name} group={group} priority={options.Priority} tag={options.Tag ?? "-"}");
        }
    }

    private async Task EXECUTE_WORKER_ASYNC(WorkerState st, Gate? gate, CancellationTokenSource cts)
    {
        string name = st.Name;
        string group = st.Group;
        ISnowflake id = st.Id;
        IWorkerOptions options = st.Options;
        WorkerOptions? concreteOptions = options as WorkerOptions;

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
                        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                        {
                            logger.LogWarning($"[FW.{nameof(TaskManager)}] worker-reject name={name} group={group} reason=group-cap");
                        }

                        _ = _workers.TryRemove(id, out _);
                        try
                        {
                            cts.Dispose();
                        }
                        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } loggerDispose && loggerDispose.IsEnabled(LogLevel.Warning))
                            {
                                loggerDispose.LogWarning($"[FW.{nameof(TaskManager)}] cts-dispose-error-reject id={id} msg={ex.Message}");
                            }
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

            int currentRunning = Interlocked.Increment(ref _runningWorkerCount);
            int peak = Volatile.Read(ref _peakRunningWorkerCount);
            while (currentRunning > peak)
            {
                _ = Interlocked.CompareExchange(ref _peakRunningWorkerCount, currentRunning, peak);
                peak = Volatile.Read(ref _peakRunningWorkerCount);
            }

            if (_options.IsEnableLatency)
            {
                long waitTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks - st.ScheduledUtc.UtcTicks;
                if (waitTicks > 0)
                {
                    _ = Interlocked.Add(ref _workerWaitTicks, waitTicks);
                }
            }

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
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            failure = ex;
            _ = Interlocked.Increment(ref _workerErrorCount);

            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError($"[FW.{nameof(TaskManager)}] worker-error id={id} name={name} msg={ex.Message}");
            }
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
            catch (Exception cbex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(cbex))
            {
                _ = Interlocked.Increment(ref _workerErrorCount);

                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"[FW.{nameof(TaskManager)}] worker-callback-error id={id} msg={cbex.Message}");
                }
            }

            if (gate is not null && acquired)
            {
                try
                {
                    _ = gate.SemaphoreSlim.Release();
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    _ = Interlocked.Increment(ref _workerErrorCount);

                    if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[FW.{nameof(TaskManager)}] gate-release-error id={id} msg={ex.Message}");
                    }
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

                    double ms = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;
                    int bucketIndex = ms switch
                    {
                        < 1.0 => 0,
                        < 5.0 => 1,
                        < 10.0 => 2,
                        < 25.0 => 3,
                        < 50.0 => 4,
                        < 100.0 => 5,
                        < 250.0 => 6,
                        < 500.0 => 7,
                        < 1000.0 => 8,
                        < 2500.0 => 9,
                        _ => 10
                    };
                    _ = Interlocked.Increment(ref _workerLatencyBuckets[bucketIndex]);
                }
            }

            this.RETAIN_OR_REMOVE(st);
            _ = _globalConcurrencyGate.Release();
        }
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
        double busyWaitMaxSeconds = _options.BusyWaitThreshold.TotalSeconds;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                long now = Clock.MonoTicksNow();
                long delayTicks = next - now;

                if (delayTicks > 0)
                {
                    double delaySeconds = (double)delayTicks / freq;

                    if (delaySeconds <= busyWaitMaxSeconds)
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
                    if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
                    {
                        logger.LogError(oce, $"[FW.{nameof(TaskManager)}:Internal] recurring-timeout name={s.Name}");
                    }

                    await this.RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    s.MarkFailure();
                    if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
                    {
                        logger.LogError(ex, $"[FW.{nameof(TaskManager)}:Internal] recurring-error name={s.Name}");
                    }

                    await this.RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
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
                        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                            {
                                logger.LogWarning($"[FW.{nameof(TaskManager)}:Internal] gate-release-error name={s.Name} msg={ex.Message}");
                            }
                        }
                    }
                    next += step;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                s.MarkFailure();
                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(ex, $"[FW.{nameof(TaskManager)}:Internal] recurring-loop-error name={s.Name}");
                }

                await this.RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
            }
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async ValueTask RECURRING_BACKOFF_ASYNC(
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
        int pow = Math.Min(_options.BackoffMaxPower, s.ConsecutiveFailures - n);
        int baseMs = (int)_options.BackoffBaseInterval.TotalMilliseconds << pow;
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException ex)
        {
            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, $"[FW.{nameof(TaskManager)}:{nameof(RECURRING_BACKOFF_ASYNC)}] backoff-cancelled");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TRACE(string message)
    {
        if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(message);
        }
    }

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
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"[FW.{nameof(TaskManager)}] retain-cts-dispose-error id={st.Id} msg={ex.Message}");
                }
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
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"[FW.{nameof(TaskManager)}] gate-dispose-error-retain group={group} msg={ex.Message}");
                }
            }
        }
    }

    private async Task MONITOR_CONCURRENCY_ASYNC(IWorkerContext ctx, CancellationToken ct)
    {
        TaskManagerOptions options = _options;

        // Số lần liên tiếp vượt ngưỡng trước khi hành động (hysteresis)
        int streakRequired = options.AdjustmentStreakRequired;

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

                    if (_highCpuStreak >= streakRequired)
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

                    if (_lowCpuStreak >= streakRequired)
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
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning($"[FW.{nameof(TaskManager)}:Internal] dynamic-adjustment-error ex={ex.Message}");
                }
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
            if (currentWallMs < _options.CpuWarmupDuration.TotalMilliseconds)
            {
                return 0.0;
            }

            // Đánh dấu đã xong warmup, cập nhật baseline một lần ngay lúc này
            Process proc0 = Process.GetCurrentProcess();
            proc0.Refresh();
            Volatile.Write(ref _lastCpuProcessorTime, (long)proc0.TotalProcessorTime.TotalMilliseconds);
            Volatile.Write(ref _lastCpuWallClockMs, currentWallMs);
            _cpuWarmupDone = true;
            return 0.0;
        }

        Process proc = Process.GetCurrentProcess();
        proc.Refresh();
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
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            // Revert on error
            _currentConcurrencyLimit = previousLimit;

            if (InstanceManager.Instance.GetExistingInstance<ILogger>() is { } logger && logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError($"[FW.TaskManager.Internal] failed-adjust-concurrency ex={ex.Message} from={previousLimit} to={newLimit}");
            }
        }
    }
}
