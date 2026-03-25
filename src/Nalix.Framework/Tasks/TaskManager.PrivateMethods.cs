// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Time;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    #region Types

    private sealed record Gate(System.Threading.SemaphoreSlim SemaphoreSlim, int Capacity);

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
    private readonly System.Diagnostics.Stopwatch _cpuMeasureStopwatch = System.Diagnostics.Stopwatch.StartNew();

    private int _lowCpuStreak;
    private int _highCpuStreak;

    private volatile bool _cpuWarmupDone;

    #endregion Fields (CPU Monitoring)

    #region Internal Cleanup

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void CLEANUP_WORKERS()
    {
        if (_disposed)
        {
            return;
        }

        System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            System.TimeSpan? keep = st.Options.RetainFor;

            if (keep is null || keep <= System.TimeSpan.Zero)
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
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[FW.{nameof(TaskManager)}] cleanup-remove-ok id={st.Id}");
                try
                {
                    st.Cts.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}] cleanup-cts-dispose-error id={st.Id} msg={ex.Message}");
                }
            }
        }
    }

    #endregion Internal Cleanup

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private async System.Threading.Tasks.Task RECURRING_LOOP_ASYNC(
        RecurringState s, System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work)
    {
        System.Threading.CancellationToken ct = s.CancellationTokenSource.Token;

        // Initial jitter (unchanged semantics)
        if (s.Options.Jitter is { } j && j > System.TimeSpan.Zero)
        {
            try
            {
                int maxMs = (int)j.TotalMilliseconds;
                if (maxMs > 0)
                {
                    System.TimeSpan jitter = System.TimeSpan.FromMilliseconds(Csprng.GetInt32(0, maxMs));
                    await System.Threading.Tasks.Task.Delay(jitter, ct).ConfigureAwait(false);
                }
            }
            catch (System.OperationCanceledException) { return; }
        }

        // Interval in Stopwatch ticks
        long step = s.IntervalTicks;
        long freq = System.Diagnostics.Stopwatch.Frequency;
        long next = System.Diagnostics.Stopwatch.GetTimestamp() + step;

        // Local helpers for fast delay
        static void BusyWait(long untilTicks, System.Threading.CancellationToken ct)
        {
            System.Threading.SpinWait sw = new();
            while (System.Diagnostics.Stopwatch.GetTimestamp() < untilTicks)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                sw.SpinOnce(sleep1Threshold: -1);
            }
        }

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
                        System.TimeSpan ts = System.TimeSpan.FromSeconds(delaySeconds);
                        await System.Threading.Tasks.Task.Delay(ts, ct)
                                                         .ConfigureAwait(false);
                    }
                }
                else
                {
                    // catch up missed intervals
                    long missed = ((-delayTicks) + step - 1) / step;
                    next += (missed + 1) * step;
                }

                if (s.Options.NonReentrant)
                {
                    if (!await s.Gate.WaitAsync(0, ct).ConfigureAwait(false))
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Debug($"[FW.{nameof(TaskManager)}:Internal] gate-acquire-fail name={s.Name}");
                        next += step;
                        continue;
                    }
                }

                try
                {
                    s.MarkStart();

                    if (s.Options.ExecutionTimeout is { } to && to > System.TimeSpan.Zero)
                    {
                        using System.Threading.CancellationTokenSource rcts =
                            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

                        rcts.CancelAfter(to);
                        await work(rcts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await work(ct).ConfigureAwait(false);
                    }

                    s.MarkSuccess();
                }
                catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (System.OperationCanceledException oce)
                {
                    s.MarkFailure();
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-timeout name={s.Name} msg={oce.Message}");

                    await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    s.MarkFailure();
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-error name={s.Name} msg={ex.Message}");

                    await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (s.Options.NonReentrant)
                    {
                        try
                        {
                            _ = s.Gate.Release();
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}:Internal] gate-release-error name={s.Name} msg={ex.Message}");
                        }
                    }
                    next += step;
                }
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (System.Exception ex)
            {
                s.MarkFailure();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-loop-error name={s.Name} msg={ex.Message}");

                await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
            }
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static async System.Threading.Tasks.ValueTask RECURRING_BACKOFF_ASYNC(
        RecurringState s,
        System.Threading.CancellationToken ct)
    {
        int n = System.Math.Max(1, s.Options.FailuresBeforeBackoff);
        if (s.ConsecutiveFailures < n)
        {
            return;
        }

        int pow = System.Math.Min(5, s.ConsecutiveFailures - n); // cap at 2^5 = 32s
        int baseMs = 1000 << pow; // base delay: 1000ms * 2^pow
        int cap = (int)System.Math.Max(1, s.Options.BackoffCap.TotalMilliseconds);
        int maxDelay = System.Math.Min(baseMs, cap);

        // Full jitter: random(0, min(base * 2^pow, cap))
        // pow = min(5, ConsecutiveFailures - FailuresBeforeBackoff)
        // baseMs = 1000ms * 2^pow, maxDelay = min(baseMs, cap)
        // Prevents thundering herd while maintaining exponential backoff
        int delayMs = Csprng.GetInt32(0, maxDelay + 1);

        try
        {
            await System.Threading.Tasks.Task.Delay(delayMs, ct).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) { }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private int COUNT_RUNNING_WORKERS()

    {
        int n = 0; foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            if (kv.Value.IsRunning)
            {
                n++;
            }
        }

        return n;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void RETAIN_OR_REMOVE(WorkerState st)
    {
        System.TimeSpan? keep = st.Options.RetainFor;
        if (keep is null || keep <= System.TimeSpan.Zero)
        {
            _ = _workers.TryRemove(st.Id, out _);

            try
            {
                st.Cts.Dispose();
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}] retain-cts-dispose-error id={st.Id} msg={ex.Message}");
            }

            bool hasSameGroup = false;
            foreach (WorkerState other in _workers.Values)
            {
                if (string.Equals(other.Group, st.Group, System.StringComparison.Ordinal))
                {
                    hasSameGroup = true;
                    break;
                }
            }

            if (!hasSameGroup && _groupGates.TryRemove(st.Group, out Gate? g))
            {
                try
                {
                    g.SemaphoreSlim.Dispose();

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[FW.{nameof(TaskManager)}] group-gate-dispose-ok group={st.Group}");
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}] gate-dispose-error-retain group={st.Group} msg={ex.Message}");
                }
            }

            return;
        }
    }

    private async System.Threading.Tasks.Task MONITOR_CONCURRENCY_ASYNC(IWorkerContext ctx, System.Threading.CancellationToken ct)
    {
        TaskManagerOptions options = _options;

        // Số lần liên tiếp vượt ngưỡng trước khi hành động (hysteresis)
        const int StreakRequired = 3;

        // Normalize threshold: config là % trên 1 core → scale lên toàn bộ core
        double coreCount = System.Environment.ProcessorCount;
        double threshHigh = options.ThresholdHighCpu * coreCount;
        double threshLow = options.ThresholdLowCpu * coreCount;

        // Khởi tạo baseline CPU trước khi vòng lặp bắt đầu
        INITIALIZE_CPU_MEASUREMENT();

        while (!ct.IsCancellationRequested && options.DynamicAdjustmentEnabled)
        {
            try
            {
                double cpuUsage = MEASURE_CPU_USAGE_PERCENT();

                if (cpuUsage > threshHigh)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[FW.{nameof(TaskManager)}:Internal] cpu-high usage={cpuUsage:F1}% threshold={threshHigh:F1}%");
                }

                // --- Hysteresis: tích streak, chỉ hành động khi đủ N lần liên tiếp ---
                if (cpuUsage > threshHigh && _currentConcurrencyLimit > 1)
                {
                    _lowCpuStreak = 0;
                    _highCpuStreak++;

                    if (_highCpuStreak >= StreakRequired)
                    {
                        _highCpuStreak = 0; // reset sau khi hành động
                        int newLimit = System.Math.Max(1, _currentConcurrencyLimit - 1);
                        ADJUST_CONCURRENCY(newLimit);
                    }
                }
                else if (cpuUsage < threshLow && _currentConcurrencyLimit < options.MaxWorkers)
                {
                    _highCpuStreak = 0;
                    _lowCpuStreak++;

                    if (_lowCpuStreak >= StreakRequired)
                    {
                        _lowCpuStreak = 0; // reset sau khi hành động
                        int newLimit = System.Math.Min(options.MaxWorkers, _currentConcurrencyLimit + 1);
                        ADJUST_CONCURRENCY(newLimit);
                    }
                }
                else
                {
                    // CPU trong vùng ổn định → reset cả hai streak
                    _highCpuStreak = 0;
                    _lowCpuStreak = 0;
                }

                ctx.Beat();
                ctx.Advance(1);

                await System.Threading.Tasks.Task.Delay(options.ObservingInterval, ct);
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}:Internal] dynamic-adjustment-error ex={ex.Message}");
            }
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void INITIALIZE_CPU_MEASUREMENT()
    {
        System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
        System.Threading.Volatile.Write(ref _lastCpuProcessorTime, (long)proc.TotalProcessorTime.TotalMilliseconds);
        System.Threading.Volatile.Write(ref _lastCpuWallClockMs, _cpuMeasureStopwatch.ElapsedMilliseconds);
        _cpuWarmupDone = false;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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
            System.Diagnostics.Process proc0 = System.Diagnostics.Process.GetCurrentProcess();
            System.Threading.Volatile.Write(ref _lastCpuProcessorTime, (long)proc0.TotalProcessorTime.TotalMilliseconds);
            System.Threading.Volatile.Write(ref _lastCpuWallClockMs, currentWallMs);
            _cpuWarmupDone = true;
            return 0.0;
        }

        System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
        long currentCpuMs = (long)proc.TotalProcessorTime.TotalMilliseconds;

        long prevWallMs = System.Threading.Volatile.Read(ref _lastCpuWallClockMs);
        long prevCpuMs = System.Threading.Volatile.Read(ref _lastCpuProcessorTime);

        long wallDelta = currentWallMs - prevWallMs;
        long cpuDelta = currentCpuMs - prevCpuMs;

        // Cập nhật baseline cho lần đo tiếp theo
        System.Threading.Volatile.Write(ref _lastCpuWallClockMs, currentWallMs);
        System.Threading.Volatile.Write(ref _lastCpuProcessorTime, currentCpuMs);

        // Tránh chia cho 0 hoặc delta âm (clock skew, process refresh lag)
        if (wallDelta <= 0 || cpuDelta < 0)
        {
            return 0.0;
        }

        double processorCount = System.Environment.ProcessorCount;

        // cpuDelta / wallDelta = tỷ lệ sử dụng trên 1 core → nhân processorCount → % trên toàn bộ core
        double cpuUsagePercent = cpuDelta / (double)wallDelta * processorCount * 100.0;

        return System.Math.Min(cpuUsagePercent, processorCount * 100.0);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ADJUST_CONCURRENCY(int newLimit)
    {
        // Safety: clamp to valid range
        newLimit = System.Math.Clamp(newLimit, 1, _options.MaxWorkers);

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
                        // Không còn slot rảnh → revert về số đã thu hồi được thực tế
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Debug($"[FW.TaskManager.Internal] concurrency-partial-retreat from={previousLimit} to={previousLimit - i}");
                        _currentConcurrencyLimit = previousLimit - i;
                        break;
                    }
                }
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[FW.TaskManager.Internal] concurrency-limit-adjusted=[{previousLimit}->{_currentConcurrencyLimit}]");
        }
        catch (System.Exception ex)
        {
            // Revert on error
            _currentConcurrencyLimit = previousLimit;

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[FW.TaskManager.Internal] failed-adjust-concurrency ex={ex.Message} from={previousLimit} to={newLimit}");
        }
    }
}
